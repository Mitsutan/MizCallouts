using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;
using System.Collections.Generic;
using System.Linq;

namespace MizCallouts.Callouts
{
    [CalloutInfo("BabyDriver", CalloutProbability.VeryLow)]

    internal class BabyDriver : Callout
    {
        private readonly string currentLanguage = Settings.CurrentLanguage;
        private Vehicle playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

        private Ped driver;
        private Ped[] shooters = new Ped[2];
        private Vehicle vehicle;
        private bool isRunningAway = false;

        private Vector3 targetBank;
        private int alarmSoundId = -1;

        private Blip suspectBlip;

        private LHandle pursuit;

        // 銀行の座標リスト
        private readonly List<Vector3> bankLocations = new List<Vector3>()
        {
            new Vector3(253.9f, 226.3f, 101.6f),    // パシフィック・スタンダード銀行（バインウッド）
            new Vector3(146.5f, -1044.8f, 29.3f),   // フリーカ銀行（レジオン・スクエア近く）
            new Vector3(-354.1f, -54.3f, 49.0f),    // フリーカ銀行（ハウィック）
            new Vector3(311.5f, -283.6f, 54.1f),    // フリーカ銀行（アルタ）
            new Vector3(1176.4f, 2712.8f, 38.0f),   // フリーカ銀行（ルート68・ハーモニー）
            new Vector3(-2956.6f, 481.3f, 15.6f),   // フリーカ銀行（グレート・オーシャン・ハイウェイ）
            new Vector3(-109.8f, 6464.9f, 31.6f)    // ブレイン郡貯蓄銀行（パレト・ベイ）
        };

        public override bool OnBeforeCalloutDisplayed()
        {
            targetBank = bankLocations.OrderBy(bank => bank.DistanceTo(Game.LocalPlayer.Character.Position)).First();
            Vector3 spawnPoint = World.GetNextPositionOnStreet(targetBank.Around(30f));

            NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(spawnPoint.X, spawnPoint.Y, spawnPoint.Z, out Vector3 streetCenter, out float streetHeading, 1, 3, 0);
            //NativeFunction.Natives.GET_SAFE_COORD_FOR_PED(targetBank.X, targetBank.Y, targetBank.Z, true, out streetCenter, 16);

            //vehicle = new Vehicle("SULTAN", streetCenter, streetHeading);
            vehicle = new Vehicle("SULTAN", spawnPoint, streetHeading);
            if (!vehicle.Exists()) return false;

            vehicle.Position = vehicle.Position + (vehicle.RightVector * 2.0f);
            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle);

            vehicle.Mods.InstallModKit();
            NativeFunction.Natives.SET_VEHICLE_MOD(vehicle, 16, 2, false);// armor
            NativeFunction.Natives.SET_VEHICLE_MOD(vehicle, 0, 0, false);
            NativeFunction.Natives.SET_VEHICLE_WHEEL_TYPE(vehicle, 0);
            NativeFunction.Natives.SET_VEHICLE_MOD(vehicle, 23, 7, false);
            NativeFunction.Natives.SET_VEHICLE_EXTRA_COLOURS(vehicle, 27, 20);
            NativeFunction.Natives.SET_VEHICLE_COLOURS(vehicle, 27, 27);

            // spawn driver
            driver = new Ped("a_m_y_ktown_01", spawnPoint, 0f);
            if (!driver.Exists()) return false;
            driver.WarpIntoVehicle(vehicle, -1);// -1 is the driver seat

            // spawn shooters
            shooters[0] = new Ped("a_f_y_bevhills_03", spawnPoint, 0f);
            if (!shooters[0].Exists()) return false;
            shooters[0].Armor = 100;
            shooters[0].WarpIntoVehicle(vehicle, 1);// 1 is the rear left seat
            shooters[0].Inventory.GiveNewWeapon("WEAPON_MICROSMG", -1, true);
            shooters[0].Accuracy = 1;

            shooters[1] = new Ped("a_m_y_smartcaspat_01", spawnPoint, 0f);
            if (!shooters[1].Exists()) return false;
            shooters[1].Armor = 100;
            shooters[1].WarpIntoVehicle(vehicle, 2);// 2 is the rear right seat
            shooters[1].Inventory.GiveNewWeapon("WEAPON_MICROSMG", -1, true);
            shooters[1].Accuracy = 1;

            this.ShowCalloutAreaBlipBeforeAccepting(spawnPoint, 30f);
            this.AddMinimumDistanceCheck(50f, spawnPoint);

            this.CalloutMessage = Settings.BabyDriver.ReadString(currentLanguage, "CalloutMessage", "銀行にて非常通報ベル鳴動");
            this.CalloutPosition = spawnPoint;

            Functions.PlayScannerAudio("ATTENTION_ALL_UNITS WE_HAVE CRIME_ARMED_ROBBERY IN_OR_ON_POSITION UNITS_RESPOND_CODE_03");

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            base.OnCalloutAccepted();

            if (vehicle.Exists())
            {
                suspectBlip = vehicle.AttachBlip();
                suspectBlip.Color = System.Drawing.Color.Red;
                suspectBlip.IsRouteEnabled = true;
            }
            return true;
        }

        public override void OnCalloutNotAccepted()
        {
            base.OnCalloutNotAccepted();
            if (driver.Exists()) driver.Delete();
            foreach (var shooter in shooters)
            {
                if (shooter.Exists()) shooter.Delete();
            }
            if (vehicle.Exists()) vehicle.Delete();
        }

        public override void Process()
        {
            base.Process();

            if (!driver.Exists() || !vehicle.Exists() || !shooters[0].Exists() || !shooters[1].Exists())
            {
                this.End();
                return;
            }

            if (Game.LocalPlayer.Character.IsInAnyVehicle(false))
            {
                Vehicle currentVehicle = Game.LocalPlayer.Character.CurrentVehicle;

                // 現在乗っている車のタイヤがパンクする設定なら
                if (currentVehicle.Exists() && currentVehicle.CanTiresBurst)
                {
                    // 途中で別のパトカーに乗り換えていたら、前のパトカーの防弾を解除
                    if (playerVehicle != null && playerVehicle.Exists() && playerVehicle != currentVehicle)
                    {
                        playerVehicle.CanTiresBurst = true;
                    }

                    // 新しい車を記憶して、防弾化する
                    playerVehicle = currentVehicle;
                    playerVehicle.CanTiresBurst = false;
                }
            }

            // 車両の性能を上げる（これにより、追跡が難しくなります）
            NativeFunction.Natives.SET_VEHICLE_CHEAT_POWER_INCREASE(vehicle, 1.1f);

            // アラーム音を鳴らす
            if (alarmSoundId == -1 && Game.LocalPlayer.Character.DistanceTo(targetBank) < 200f)
            {
                alarmSoundId = NativeFunction.Natives.GET_SOUND_ID<int>();
                Game.LogTrivial("[BabyDriver] Sound ID for alarm: " + alarmSoundId);

                GameFiber.StartNew(() =>
                {
                    uint GameTimeStarted = Game.GameTime;

                    // "Alarms"バンクが完全にメモリに乗るまで待機（最大2秒）
                    while (!NativeFunction.Natives.REQUEST_SCRIPT_AUDIO_BANK<bool>("Alarms", false, -1) && Game.GameTime - GameTimeStarted <= 2000)
                    {
                        GameFiber.Yield();
                    }

                    NativeFunction.Natives.PLAY_SOUND_FROM_COORD(
                        alarmSoundId,
                        "Burglar_Bell",
                        targetBank.X, targetBank.Y, targetBank.Z,
                        "Generic_Alarms",
                        false, 0, false
                    );
                });
            }

            // 逃走開始
            if (pursuit == null && Game.LocalPlayer.Character.DistanceTo(vehicle) < 100f && !isRunningAway)
            {
                driver.Tasks.CruiseWithVehicle(35f, VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.DriveAroundVehicles);
                isRunningAway = true;
            }

            // 追跡開始
            if (pursuit == null && Game.LocalPlayer.Character.DistanceTo(vehicle) < 40f) StartPursuit();

            if (driver.IsDead || (vehicle.Speed < 1f && !IsDriving(driver) && pursuit != null))
            {
                if (shooters[0].IsInAnyVehicle(false)) shooters[0].Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                if (shooters[1].IsInAnyVehicle(false)) shooters[1].Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);

                Functions.SetPursuitDisableAIForPed(shooters[0], false);
                Functions.SetPursuitDisableAIForPed(shooters[1], false);

                shooters[0].Tasks.FightAgainst(Game.LocalPlayer.Character);
                shooters[1].Tasks.FightAgainst(Game.LocalPlayer.Character);
            }

            if (pursuit != null && !Functions.IsPursuitStillRunning(pursuit))
            {
                Functions.PlayScannerAudio("ATTENTION_DISPATCH WE_ARE_CODE_4 NO_FURTHER_UNITS_REQUIRED");
                this.End();
                return;
            }
        }

        private static bool IsDriving(Ped ped)
        {
            if (!ped.Exists()) return false;

            bool d = NativeFunction.Natives.GET_IS_TASK_ACTIVE<bool>(ped, 169);
            return d;
        }

        public override void End()
        {
            base.End();

            if (alarmSoundId != -1)
            {
                NativeFunction.Natives.STOP_SOUND(alarmSoundId);
                NativeFunction.Natives.RELEASE_SOUND_ID(alarmSoundId);
                alarmSoundId = -1;
            }

            NativeFunction.Natives.RELEASE_NAMED_SCRIPT_AUDIO_BANK("Alarms");

            if (suspectBlip.Exists()) suspectBlip.Delete();
            if (driver.Exists()) driver.Dismiss();
            foreach (var shooter in shooters)
            {
                if (shooter.Exists()) shooter.Dismiss();
            }
            if (vehicle.Exists()) vehicle.Dismiss();

            if (playerVehicle != null && playerVehicle.Exists())
            {
                playerVehicle.CanTiresBurst = true;
                Game.LogTrivial("[BabyDriver] プレイヤー車両のタイヤの防弾設定を解除しました。");
            }
        }

        private void StartPursuit()
        {
            if (suspectBlip.Exists()) suspectBlip.Delete();


            pursuit = Functions.CreatePursuit();
            Functions.SetPursuitAsCalledIn(pursuit, true);

            // 追跡にドライバーを追加
            Functions.AddPedToPursuit(pursuit, driver);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(driver, 17, true);// AlwaysFlee
            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(driver, 2, true);// use vehicle
            PedPursuitAttributes driverAttributes = Functions.GetPedPursuitAttributes(driver);
            // ハンドリング能力を向上させる（事故りにくくなる）
            driverAttributes.HandlingAbility = 2.0f;
            driverAttributes.HandlingAbilityTurns = 2.0f;

            // 車が大破した時の降伏確率をゼロにする
            driverAttributes.SurrenderChanceCarBadlyDamaged = 0f;
            // PITマニューバ（体当たり）を受けた際の各種降伏確率をゼロにする
            driverAttributes.SurrenderChancePitted = 0f;
            driverAttributes.SurrenderChancePittedAndCrashed = 0f;
            driverAttributes.SurrenderChancePittedAndSlowedDown = 0f;
            // タイヤがバーストした際の降伏確率をゼロにする
            driverAttributes.SurrenderChanceTireBurst = 0f;
            driverAttributes.SurrenderChanceTireBurstAndCrashed = 0f;
            driverAttributes.AverageBurstTireSurrenderTime = int.MaxValue;
            // 時間経過による自然な降伏までの平均時間を無限（極大値）にする
            driverAttributes.AverageSurrenderTime = int.MaxValue;

            // 追跡にシューターを追加し、LSPDFRAIを無効化する
            Functions.AddPedToPursuit(pursuit, shooters[0]);
            Functions.SetPursuitDisableAIForPed(shooters[0], true);
            Functions.AddPedToPursuit(pursuit, shooters[1]);
            Functions.SetPursuitDisableAIForPed(shooters[1], true);

            shooters[0].RelationshipGroup = RelationshipGroup.HatesPlayer;
            shooters[1].RelationshipGroup = RelationshipGroup.HatesPlayer;
            shooters[0].RelationshipGroup.SetRelationshipWith(Game.LocalPlayer.Character.RelationshipGroup, Relationship.Hate);
            shooters[1].RelationshipGroup.SetRelationshipWith(Game.LocalPlayer.Character.RelationshipGroup, Relationship.Hate);

            shooters[0].Tasks.FightAgainst(Game.LocalPlayer.Character);
            shooters[1].Tasks.FightAgainst(Game.LocalPlayer.Character);

            // Attribute 2 = Can do drivebys (ドライブバイ許可)
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[0], 2, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[1], 2, true);
            // Attribute 5 = Always fight (危険が迫っても絶対に怯えずに戦う)
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[0], 5, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[1], 5, true);
            // Attribute 46 = Always fight (戦い続ける)
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[0], 46, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[1], 46, true);
            // BF_DisableFleeFromCombat = 58
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[0], 58, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(shooters[1], 58, true);

            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
        }
    }
}
