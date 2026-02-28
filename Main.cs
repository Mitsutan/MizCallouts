using LSPD_First_Response.Mod.API;
using Rage;

namespace MizCallouts
{
    public class Main : Plugin
    {
        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            Settings.Load();
            Game.LogTrivial("[MizCallouts] Plugin initialized.");
        }
        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            if (onDuty)
            {
                Functions.RegisterCallout(typeof(Callouts.BabyDriver));
                Game.DisplayNotification("[MizCallouts] Callout loaded successfully!");
            }
        }

        public override void Finally()
        {
            Functions.OnOnDutyStateChanged -= OnOnDutyStateChangedHandler;
            Game.LogTrivial("[MizCallouts] Plugin unloaded.");
        }
    }
}
