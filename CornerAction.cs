using System.Runtime.Serialization;

namespace HotCornersWin
{
    [DataContract]
    public enum CornerAction
    {
        [EnumMember] None,
        [EnumMember] ShowDesktop,
        [EnumMember] TaskView,
        [EnumMember] StartMenu,
        [EnumMember] ActionCenter,
        [EnumMember] VolumeUp,
        [EnumMember] VolumeDown,
        [EnumMember] ShowTaskbar,
        [EnumMember] LockScreen,
        [EnumMember] CustomShortcut
    }
}
