using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

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

        public void PlayRandomPudgeSound()
        {
            PlayRandomSoundByToken("Pudge");
        }

        public void PlayRandomRubickSound()
        {
            PlayRandomSoundByToken("Rubick");
        }

        public void PlayRandomSlarkSound()
        {
            PlayRandomSoundByToken("Slark");
        }

        public void PlayParticularDotaSound(DotaSoundType soundType)
        {
            switch (soundType)
            {
                case DotaSoundType.DotaFirstBlood:
                    PlayDotaSound(Properties.Resources.DotaFirstBlood);
                    break;
                case DotaSoundType.DotaDoubleKill:
                    PlayDotaSound(Properties.Resources.DotaDoubleKill);
                    break;
                case DotaSoundType.DotaTrippleKill:
                    PlayDotaSound(Properties.Resources.DotaTrippleKill);
                    break;
                case DotaSoundType.DotaUltraKill:
                    PlayDotaSound(Properties.Resources.DotaUltraKill);
                    break;
                case DotaSoundType.DotaRampage:
                    PlayDotaSound(Properties.Resources.DotaRampage);
                    break;
                case DotaSoundType.DotaHolyShit:
                    PlayDotaSound(Properties.Resources.DotaHolyShit);
                    break;
                case DotaSoundType.DotaOwnage:
                    PlayDotaSound(Properties.Resources.DotaOwnage);
                    break;
                case DotaSoundType.PudgeAuuup:
                    PlayDotaSound(Properties.Resources.PudgeAyyup);
                    break;
                case DotaSoundType.PudgeBloodyCreeps:
                    PlayDotaSound(Properties.Resources.PudgeBloodyCreeps);
                    break;
                case DotaSoundType.PudgeChopChop:
                    PlayDotaSound(Properties.Resources.PudgeChopChop);
                    break;
                case DotaSoundType.PudgeChopperTime:
                    PlayDotaSound(Properties.Resources.PudgeChopperTime);
                    break;
                case DotaSoundType.PudgeComeToPudge:
                    PlayDotaSound(Properties.Resources.PudgeComeToPudge);
                    break;
                case DotaSoundType.PudgeFreshChops:
                    PlayDotaSound(Properties.Resources.PudgeFreshChops);
                    break;
                case DotaSoundType.PudgeGetOverHere:
                    PlayDotaSound(Properties.Resources.PudgeGetOverHere);
                    break;
                case DotaSoundType.PudgeGetOverHereTough:
                    PlayDotaSound(Properties.Resources.PudgeGetOverHere_tough);
                    break;
                case DotaSoundType.PudgeHahaFreshMeat:
                    PlayDotaSound(Properties.Resources.PudgeHahaFreshMeat);
                    break;
                case DotaSoundType.PudgeHahahaFreshMeat:
                    PlayDotaSound(Properties.Resources.PudgeHahahaFreshMeat);
                    break;
                case DotaSoundType.RubickLaughNice:
                    PlayDotaSound(Properties.Resources.RubickLaughNice);
                    break;
                case DotaSoundType.RubickWhoops:
                    PlayDotaSound(Properties.Resources.RubickWhoops);
                    break;
                case DotaSoundType.RubickAhahahahmhmha:
                    PlayDotaSound(Properties.Resources.RubickAhahahahmhmha);
                    break;
                case DotaSoundType.RubickNyahaheheha:
                    PlayDotaSound(Properties.Resources.RubickNyahaheheha);
                    break;
                case DotaSoundType.SlarkFishyFishy:
                    PlayDotaSound(Properties.Resources.SlarkFishyFishy);
                    break;
                case DotaSoundType.SlarkGotcha:
                    PlayDotaSound(Properties.Resources.SlarkGotcha);
                    break;
                case DotaSoundType.SlarkIGotThisOne:
                    PlayDotaSound(Properties.Resources.SlarkIGotThisOne);
                    break;
            }
        }

        private void PlayRandomSoundByToken(string token)
        {
            var values = Enum.GetNames(typeof(DotaSoundType)).Where(s => s.Contains(token));
            string[] arr = values.ToArray();
            var r = new Random();
            var randomIndex = r.Next(0, arr.Length);

            if (randomIndex >= arr.Length)
                randomIndex--;

            if (randomIndex < 0)
                randomIndex++;

            var soDotaSoundIs = (DotaSoundType)Enum.Parse(typeof(DotaSoundType), arr[randomIndex]);

            PlayParticularDotaSound(soDotaSoundIs);

        }

        private void PlayDotaSound(UnmanagedMemoryStream soundStream)
        {
            SoundPlayer player = new SoundPlayer(soundStream);
            player.LoadCompleted += (sender, args) => player.Play();
            player.LoadAsync();
        }
    }
}
