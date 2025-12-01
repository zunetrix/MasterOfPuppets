// private void MoveToCommand(string[] args, bool relativeToPlayer, bool fly) {
//     var originActor = relativeToPlayer ? Service.ClientState.LocalPlayer : null;
//     var origin = originActor?.Position ?? new();
//     var offset = new Vector3(
//         float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture),
//         float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture),
//         float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
//     _asyncMove.MoveTo(origin + offset, fly);
// }


// /*
//                    +Z
//                     |
//                     |
//                     |
//       +X -----------+----------- -X
//                     |
//                     |
//                     |
//                    -Z
//                    /
//                   /
//                  /
//              +Y /
//                 (fly up)

//               -Y (fly down)
// */
