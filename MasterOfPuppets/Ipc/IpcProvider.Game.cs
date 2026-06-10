using System.Numerics;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {
    public void ExecuteAbandonDuty() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteAbandonDuty).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteAbandonDuty)]
    private void HandleExecuteAbandonDuty(IpcMessage message) {
        GameFunctions.AbandonDuty();
    }

    public void EnableCamHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.EnableCamHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.EnableCamHack)]
    private void HandleEnableCamHack(IpcMessage message) {
        GameCameraManager.EnableCamHighHeight();
    }

    public void DisableCamHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.DisableCamHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.DisableCamHack)]
    private void HandleDisableCamHack(IpcMessage message) {
        GameCameraManager.Disable();
    }

    public void EnableRenderHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.EnableRenderHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.EnableRenderHack)]
    private void HandleEnableRenderHack(IpcMessage message) {
        Plugin.GameRenderManager.DisableRendering(true);
    }

    public void DisableRenderHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.DisableRenderHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.DisableRenderHack)]
    private void HandleDisableRenderHack(IpcMessage message) {
        Plugin.GameRenderManager.DisableRendering(false);
    }

    public void BroadcastMyFlagMapMarker() {
        var flagPos = GameMapManager.GetFlagPosition();
        if (flagPos == null) return;

        var mark = new MapMark {
            MapId = DalamudApi.ClientState.MapId,
            TerritoryId = DalamudApi.ClientState.TerritoryType,
            Position = flagPos.Value,
        };

        var payload = mark.JsonSerialize();
        BroadCast(IpcMessage.Create(IpcMessageType.SetFlagMapMarker, payload).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.SetFlagMapMarker)]
    private void HandleSetFlagMapMarker(IpcMessage message) {
        var mapMark = message.StringData[0].JsonDeserialize<MapMark>();
        GameMapManager.SetFlagMapMarker(mapMark.TerritoryId, mapMark.MapId, mapMark.Position);
    }

    private class MapMark {
        public uint MapId;
        public uint TerritoryId;
        public Vector2 Position;
    }
}
