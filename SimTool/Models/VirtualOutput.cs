// Models/VirtualOutput.cs
#nullable enable
namespace SimTools.Models
{
    // Virtual outputs your app can emit (vJoy buttons).
    // Keep 'None' for "no output".
    public enum VirtualOutput
    {
        None = 0,

        // vJoy buttons (1..128)
        VJoy1 = 1, VJoy2 = 2, VJoy3 = 3, VJoy4 = 4, VJoy5 = 5, VJoy6 = 6, VJoy7 = 7, VJoy8 = 8,
        VJoy9 = 9, VJoy10 = 10, VJoy11 = 11, VJoy12 = 12, VJoy13 = 13, VJoy14 = 14, VJoy15 = 15, VJoy16 = 16,
        VJoy17 = 17, VJoy18 = 18, VJoy19 = 19, VJoy20 = 20, VJoy21 = 21, VJoy22 = 22, VJoy23 = 23, VJoy24 = 24,
        VJoy25 = 25, VJoy26 = 26, VJoy27 = 27, VJoy28 = 28, VJoy29 = 29, VJoy30 = 30, VJoy31 = 31, VJoy32 = 32,
        VJoy33 = 33, VJoy34 = 34, VJoy35 = 35, VJoy36 = 36, VJoy37 = 37, VJoy38 = 38, VJoy39 = 39, VJoy40 = 40,
        VJoy41 = 41, VJoy42 = 42, VJoy43 = 43, VJoy44 = 44, VJoy45 = 45, VJoy46 = 46, VJoy47 = 47, VJoy48 = 48,
        VJoy49 = 49, VJoy50 = 50, VJoy51 = 51, VJoy52 = 52, VJoy53 = 53, VJoy54 = 54, VJoy55 = 55, VJoy56 = 56,
        VJoy57 = 57, VJoy58 = 58, VJoy59 = 59, VJoy60 = 60, VJoy61 = 61, VJoy62 = 62, VJoy63 = 63, VJoy64 = 64,
        VJoy65 = 65, VJoy66 = 66, VJoy67 = 67, VJoy68 = 68, VJoy69 = 69, VJoy70 = 70, VJoy71 = 71, VJoy72 = 72,
        VJoy73 = 73, VJoy74 = 74, VJoy75 = 75, VJoy76 = 76, VJoy77 = 77, VJoy78 = 78, VJoy79 = 79, VJoy80 = 80,
        VJoy81 = 81, VJoy82 = 82, VJoy83 = 83, VJoy84 = 84, VJoy85 = 85, VJoy86 = 86, VJoy87 = 87, VJoy88 = 88,
        VJoy89 = 89, VJoy90 = 90, VJoy91 = 91, VJoy92 = 92, VJoy93 = 93, VJoy94 = 94, VJoy95 = 95, VJoy96 = 96,
        VJoy97 = 97, VJoy98 = 98, VJoy99 = 99, VJoy100 = 100, VJoy101 = 101, VJoy102 = 102, VJoy103 = 103, VJoy104 = 104,
        VJoy105 = 105, VJoy106 = 106, VJoy107 = 107, VJoy108 = 108, VJoy109 = 109, VJoy110 = 110, VJoy111 = 111, VJoy112 = 112,
        VJoy113 = 113, VJoy114 = 114, VJoy115 = 115, VJoy116 = 116, VJoy117 = 117, VJoy118 = 118, VJoy119 = 119, VJoy120 = 120,
        VJoy121 = 121, VJoy122 = 122, VJoy123 = 123, VJoy124 = 124, VJoy125 = 125, VJoy126 = 126, VJoy127 = 127, VJoy128 = 128
    }
}
