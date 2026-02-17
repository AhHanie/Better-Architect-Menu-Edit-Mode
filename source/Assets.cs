using UnityEngine;
using Verse;

namespace Better_Architect_Edit_mode
{
    [StaticConstructorOnStartup]
    public static class Assets
    {
        public static readonly Texture2D EditIcon = ContentFinder<Texture2D>.Get("BAMEditMode/Edit");
        public static readonly Texture2D EditIconHighlighted = ContentFinder<Texture2D>.Get("BAMEditMode/EditHighlighted");
        public static readonly Texture2D RestoreIcon = ContentFinder<Texture2D>.Get("BAMEditMode/Restore");
    }
}
