using System.Linq;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets;

public partial class FormationWindow {
    private void GenerateShape(Formation formation) {
        if (!_appendMode)
            formation.Points.Clear();

        var newPoints = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = _shapeType,
            Count = _shapeN,
            Radius = _shapeRadius,
            Radius2 = _shapeRadius2,
            Width = _shapeWidth,
            Depth = _shapeDepth,
            Spacing = _shapeSpacing,
            AngleOffsetDegrees = _shapeAngleOff,
            IntParameter = _shapeParamInt,
            FaceMode = (FormationShapeFaceMode)_faceMode,
            AnchorMode = (FormationShapeAnchorMode)_shapeAnchorMode,
            AssignedCids = GetShapeAssignmentGroup()?.Cids,
        });

        formation.Points.AddRange(newPoints);

        _selPoint = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private CidGroup? GetShapeAssignmentGroup() =>
        string.IsNullOrWhiteSpace(_shapeAssignGroupSelected)
            ? null
            : Plugin.Config.CidsGroups.FirstOrDefault(g => g.Name.Equals(_shapeAssignGroupSelected, System.StringComparison.OrdinalIgnoreCase));
}
