using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using KTrie;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets.Util.ImGuiExt.AutoComplete;

/// <summary>
/// Index to help look up completion information (i.e. Auto Translate)
/// </summary>
/// <remarks>
/// Heavily inspired by Chat2: https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/Util/AutoTranslate.cs#L70
/// </remarks>
public class CompletionIndex {
    /// <summary>
    /// The `GroupTitle` field of most completion entries is empty, if we want to know the group title
    /// we need to look at the "special" group header completion, there should be 1 per GroupId.
    /// </summary>
    private readonly Dictionary<uint, Completion> CompletionGroupsById = new();
    private readonly TrieDictionary<List<CompletionInfo>> CompletionsByText = new();
    private readonly Dictionary<(uint, uint), CompletionInfo> CompletionInfoByGroupKey = new();

    private static readonly IReadOnlySet<int> AllowedCompletionGroups =
        new HashSet<int>
        {
            // 49, // mount
            55, // general actions
            56, // actions skills
            59, // pet actions
            69, // blue mage
            // 65, // minon
            // 62, // text command
        };

    public enum IndexState { UNINDEXED, INDEXING, INDEXED }
    public IndexState State { get; private set; } = IndexState.UNINDEXED;

    public IEnumerable<CompletionInfo> Search(string prefix) {
        if (State != IndexState.INDEXED) { return new List<CompletionInfo>(); }
        if (prefix == string.Empty) { return new List<CompletionInfo>(); }

        // return CompletionsByText.StartsWith(prefix.ToLower()).SelectMany(c => c.Value);
        return CompletionsByText
        .Where(c => c.Key.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
        .SelectMany(c => c.Value);
    }

    public CompletionInfo? ById(uint group, uint key) {
        if (State != IndexState.INDEXED) { return null; }

        return CompletionInfoByGroupKey.GetValueOrDefault((group, key));
    }

    public CompletionIndex() {
        StartIndexing();
    }

    public void StartIndexing() {
        if (State != IndexState.UNINDEXED) { return; }

        Task.Run(() => {
            try {
                State = IndexState.INDEXING;
                RefreshCompletionGroupIndex();

                var completions = AllCompletionInfo();
                RefreshCompletionIndex(completions);
                RefreshCompletionInfoByGroupKey(completions);
                State = IndexState.INDEXED;
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error($"Failed to index completions\n{ex}");
            }
        });
    }

    private void RefreshCompletionGroupIndex() {
        var completionGroups = DalamudApi.DataManager.GetExcelSheet<Completion>()
        .Where(c => AllowedCompletionGroups.Contains(c.Group))
        .Where(c => {
            var lookupTable = c.LookupTable.ExtractText();
            return lookupTable != "";
        });

        foreach (var cg in completionGroups) {
            CompletionGroupsById[cg.Group] = cg;
        }
    }

    private List<CompletionInfo> AllCompletionInfo() {
        var completionInfo = DalamudApi.DataManager.GetExcelSheet<Completion>()
            // .Where(raw => raw.Group != 0)
            .Where(raw => AllowedCompletionGroups.Contains(raw.Group))
            .Select(raw => ParsedCompletion.From(raw))
            .SelectMany<ParsedCompletion, CompletionInfo>(parsed => CompletionInfo.From(parsed))
            .Select(info => {
                if (CompletionGroupsById.TryGetValue(info.Group, out var completionGroup)) {
                    return info with { GroupTitle = completionGroup.GroupTitle };
                }
                return info;
            })
            .Concat(EmoteHelper.GetAllowedItems().Select(x => {
                var completion = new CompletionInfo(
                            Group: 0,
                            GroupTitle: "Emotes",
                            Key: x.ActionId,
                            SeString: x.TextCommand,
                            HelpText: x.ActionName
                        );
                return completion;
            }))
            .Concat(ItemHelper.GetAllowedItems().Select(x => {
                var completion = new CompletionInfo(
                        Group: 1,
                        GroupTitle: "Items",
                        Key: x.ActionId,
                        SeString: x.TextCommand,
                        HelpText: x.ActionName
                    );
                return completion;
            }))
             .Concat(MopMacroActionsHelper.Actions.Select((x, idx) => {
                 var completion = new CompletionInfo(
                         Group: 2,
                         GroupTitle: x.Category.ToString(),
                         Key: (uint)idx,
                         SeString: x.SuggestionCommand,
                         HelpText: $"{x.Example} \n {x.Notes}"
                     );
                 return completion;
             }))
            .ToList();

        return completionInfo;
    }

    private void RefreshCompletionIndex(IEnumerable<CompletionInfo> completions) {
        var grouped = completions.GroupBy(c => c.SeString.ExtractText().ToLower());
        foreach (var g in grouped) {
            CompletionsByText.Add(g.Key, g.ToList());
        }
    }

    private void RefreshCompletionInfoByGroupKey(IEnumerable<CompletionInfo> completions) {
        foreach (var completion in completions) {
            CompletionInfoByGroupKey[(completion.Group, completion.Key)] = completion;
        }
    }
}
