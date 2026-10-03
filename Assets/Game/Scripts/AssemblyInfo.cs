using System.Runtime.CompilerServices;

// Tests raise sim events directly (the Raise* methods are internal so gameplay code cannot
// fake an event), and drive presentation internals such as CharacterView.ApplyRecoil without
// a frame loop. Only the two test assemblies get this access.
[assembly: InternalsVisibleTo("BorrowedHex.Tests.EditMode")]
[assembly: InternalsVisibleTo("BorrowedHex.Tests.PlayMode")]
