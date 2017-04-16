namespace OptionsThugs.Model.Service
{
    public class SoundManager
    {
        private static SoundManager Instance;
        private SoundManager()
        {

        }

        public static SoundManager GetInstance()
        {
            return Instance ?? new SoundManager();
        }

        public static void PlayDotaSound(DotaSoundType soundType)
        {
            switch (soundType)
            {
                case DotaSoundType.DotaDoubleKill:
                    break;
                case DotaSoundType.DotaFirstBlood:
                    break;
                //....
            }
        }
    }
}
