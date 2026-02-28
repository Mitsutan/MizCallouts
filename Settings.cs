using Rage;

namespace MizCallouts
{
    internal static class Settings
    {
        public static InitializationFile BabyDriver { get; private set; }
        public static string CurrentLanguage { get; private set; } = "en";

        internal static void Load()
        {
            Game.LogTrivial("[BabyDriver] 設定ファイルと翻訳データを読み込みます...");

            string mainIniPath = "Plugins/LSPDFR/MizCallouts/MizCallouts.ini";

            // 1. メイン言語設定の読み込み
            if (System.IO.File.Exists(mainIniPath))
            {
                InitializationFile mainIni = new InitializationFile(mainIniPath);
                mainIni.Create();
                CurrentLanguage = mainIni.ReadString("Settings", "Language", "en");
                Game.LogTrivial("[MizCallouts] 言語設定 [" + CurrentLanguage + "] で読み込みが完了しました。");
            }
            else
            {
                Game.LogTrivial("[MizCallouts] メイン設定ファイルが見つかりませんでした。デフォルトの言語 [en] を使用します。");
            }

            // 2. 翻訳データの読み込み
            string babydriverIniPath = "Plugins/LSPDFR/MizCallouts/BabyDriver.ini";
            if (System.IO.File.Exists(babydriverIniPath))
            {
                Game.LogTrivial("[MizCallouts] BabyDriver.ini を読み込みます...");
                BabyDriver = new InitializationFile(babydriverIniPath);
                BabyDriver.Create();
            }
            else
            {
                Game.LogTrivial("[MizCallouts] BabyDriver.ini が見つかりませんでした。");
                throw new System.IO.FileNotFoundException("BabyDriver.ini が見つかりませんでした。");
            }

        }
    }
}
