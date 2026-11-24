using System.Runtime.CompilerServices;

// Test EditMode nằm trong Assets/Tests/Editor (biên dịch vào Assembly-CSharp-Editor)
// cần dùng các điểm nối internal của SaveManager (KeyPrefix, ClockOverride).
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
