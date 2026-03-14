// Tips by Yasd, Thank you for your help!

using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MizCallouts.Callouts
{
    [CalloutInfo("BabyDriver", CalloutProbability.Low)] // I Wouldnt call any callout very low, this means it will probably never appear

    internal class BabyDriver : Callout
    {
        readonly string currentLanguage = Settings.CurrentLanguage;
        Vehicle playerVehicle = Player.CurrentVehicle;
        static Ped Player => Game.LocalPlayer.Character;

        Ped driver;
        Ped[] shooters = new Ped[2];
        Vehicle vehicle;

        bool isRunningAway;

        bool isOnScene;

        Vector3 targetBank;
        int alarmSoundId = -1;

        Blip suspectBlip;

        LHandle pursuit;

        // 銀行の座標リスト
        readonly List<Vector3> bankLocations = new List<Vector3>()
        {
            new Vector3(231.5119f, 215.0855f, 106.2802f),// パシフィック・スタンダード銀行（バインウッド）
            new Vector3(150.9131f, -1037.258f, 29.33927f),// フリーカ銀行（レジオン・スクエア近く）
            new Vector3(-349.7284f, -46.26683f, 49.03683f),// フリーカ銀行（ハウィック）
            new Vector3(315.5833f, -275.8853f, 53.92448f),// フリーカ銀行（アルタ）
            new Vector3(1175.242f, 2703.066f, 38.17268f),// フリーカ銀行（ルート68・ハーモニー）
            new Vector3(-2966.194f, 482.478f, 15.69272f),// フリーカ銀行（グレート・オーシャン・ハイウェイ）
            new Vector3(-110.9397f, 6462.727f, 31.64072f),// ブレイン郡貯蓄銀行（パレト・ベイ）
        };

        public override bool OnBeforeCalloutDisplayed()
        {
            targetBank = bankLocations.OrderBy(bank => bank.DistanceTo(Player.Position)).First();
            this.CalloutPosition = World.GetNextPositionOnStreet(targetBank.Around(30f));

            this.ShowCalloutAreaBlipBeforeAccepting(this.CalloutPosition, 30f);
            this.AddMinimumDistanceCheck(50f, this.CalloutPosition);

            this.CalloutMessage = Settings.BabyDriver.ReadString(currentLanguage, "CalloutMessage");

            Functions.PlayScannerAudio("ATTENTION_ALL_UNITS WE_HAVE CRIME_ARMED_ROBBERY IN_OR_ON_POSITION UNITS_RESPOND_CODE_03");

            // base.Something should always be on the end instead of the top!
            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            suspectBlip = new Blip(this.CalloutPosition, 150f)
            {
                Alpha = 0.5f,
                Color = Color.Red,
                IsRouteEnabled = true
            };

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted()
        {
            
            if (driver) driver.Delete();
            foreach (var shooter in shooters)
            {
                if (shooter) shooter.Delete();
            }
            if (vehicle) vehicle.Delete();

            base.OnCalloutNotAccepted();
        }

        public override void Process()
        {
            if (!isOnScene)
            {
                if (Player.DistanceTo(targetBank) < 200f)
                {
                    isOnScene = CreateScene();
                    if (!isOnScene)
                    {
                        Game.DisplayNotification("~r~Error~w~: Failed to create the scene. Ending callout.");
                        this.End();
                        // you dont need to call return; here, when calling end Process() wont run anymore
                    }

                    // music
                    GameFiber.StartNew(() =>
                    {
                        uint GameTimeStarted = Game.GameTime;

                        while (!NativeFunction.Natives.PREPARE_MUSIC_EVENT<bool>("FH2A_GETAWAY_DRIVE_MA") && Game.GameTime - GameTimeStarted <= 2000)
                        {
                            GameFiber.Yield();
                        }

                        NativeFunction.Natives.TRIGGER_MUSIC_EVENT("FH2A_GETAWAY_DRIVE_MA");
                    });

                    // アラーム音を鳴らす
                    if (alarmSoundId == -1)
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
                                targetBank, // you can just call any vector3 directly instead of x y & z, rph handles that for you, just looks cleaner; .X, targetBank.Y, targetBank.Z,
                                "Generic_Alarms",
                                false, 0, false
                            );
                        });
                    }
                    return;
                }
            }


            if (isOnScene && (!driver || !vehicle || !shooters[0] || !shooters[1]))
            {
                this.End();
            }

            if (Player.IsInAnyVehicle(false))
            {
                Vehicle currentVehicle = Player.CurrentVehicle;

                // 現在乗っている車のタイヤがパンクする設定なら
                if (currentVehicle && currentVehicle.CanTiresBurst)
                {
                    // 途中で別のパトカーに乗り換えていたら、前のパトカーの防弾を解除
                    // its important to mention: an entity can be an Object, Ped or a Vehicle
                    // if (Suspect.Exists()) is the same as if (Suspect) without .Exists(), it just again looks cleaner
                    // if (Suspect) is the same as if (Suspect != null && Suspect.IsValid), preferably use the first, is easier :)
                    // the null check is used if the Suspect ever was created ingame and
                    // .IsValid is a gta native call for if the Suspect exists ingame and is valid/ accessable
                    if (playerVehicle && playerVehicle != currentVehicle) 
                    {
                        playerVehicle.CanTiresBurst = true;
                    }

                    // 新しい車を記憶して、防弾化する
                    playerVehicle = currentVehicle;
                    playerVehicle.CanTiresBurst = false;
                }
            }

            // 車両の性能を上げる（これにより、追跡が難しくなります）
            // I think this will crash your callout right after accepting it, if the player is further than 200f away from the callout scene,
            // because then the stuff like the vehicle is not spawned yet, so my fix is to call if (vehicle)
            if (vehicle) NativeFunction.Natives.SET_VEHICLE_CHEAT_POWER_INCREASE(vehicle, 1.1f);

            // 逃走開始
            // the same here with the vehicle as above
            if (pursuit == null && vehicle && Player.DistanceTo(vehicle) < 100f && !isRunningAway)
            {
                NativeFunction.Natives.SET_GAMEPLAY_VEHICLE_HINT(vehicle, 0f, 0f, 0f, true, 2500, 2000, 2000);
                if (driver) // using if driver for safety reasons :)
                    driver.Tasks.CruiseWithVehicle(40f, VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.DriveAroundVehicles);
                isRunningAway = true;
            }

            // 追跡開始
            if (pursuit == null && vehicle && Player.DistanceTo(vehicle) < 40f) StartPursuit();

            if (driver && driver.IsDead)
            {
                if (!shooters[0] || !shooters[1]) this.End();
                if (shooters[0].IsInAnyVehicle(false)) shooters[0].Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                if (shooters[1].IsInAnyVehicle(false)) shooters[1].Tasks.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);

                Functions.SetPursuitDisableAIForPed(shooters[0], false);
                Functions.SetPursuitDisableAIForPed(shooters[1], false);

                shooters[0].Tasks.FightAgainst(Player);
                shooters[1].Tasks.FightAgainst(Player);
            }

            if (pursuit != null && !Functions.IsPursuitStillRunning(pursuit))
            {
                Functions.PlayScannerAudio("ATTENTION_DISPATCH WE_ARE_CODE_4 NO_FURTHER_UNITS_REQUIRED");
                this.End();
            }

            base.Process();
        }

        public override void End()
        {
            //Events.OnPursuitPedHasVisualChanged -= VisualLostHandler;

            if (alarmSoundId != -1)
            {
                NativeFunction.Natives.STOP_SOUND(alarmSoundId);
                NativeFunction.Natives.RELEASE_SOUND_ID(alarmSoundId);
                alarmSoundId = -1;
            }

            NativeFunction.Natives.RELEASE_NAMED_SCRIPT_AUDIO_BANK("Alarms");

            if (suspectBlip) suspectBlip.Delete();
            if (driver) driver.Dismiss();
            foreach (var shooter in shooters)
            {
                if (shooter) shooter.Dismiss();
            }
            if (vehicle) vehicle.Dismiss();

            if (playerVehicle)
            {
                playerVehicle.CanTiresBurst = true;
                Game.LogTrivial("[BabyDriver] プレイヤー車両のタイヤの防弾設定を解除しました。");
            }

            base.End();
        }

        bool CreateScene()
        {
            NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(this.CalloutPosition, out Vector3 _, out float streetHeading, 1, 3, 0);
            //NativeFunction.Natives.GET_SAFE_COORD_FOR_PED(targetBank.X, targetBank.Y, targetBank.Z, true, out streetCenter, 16);

            //vehicle = new Vehicle("SULTAN", streetCenter, streetHeading);
            vehicle = new Vehicle("SULTAN", this.CalloutPosition, streetHeading);
            if (!vehicle) return false;

            if (suspectBlip) suspectBlip.Delete();
            suspectBlip = vehicle.AttachBlip();

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
            driver = new Ped("a_m_y_ktown_01", this.CalloutPosition, 0f);
            if (!driver) return false;
            driver.WarpIntoVehicle(vehicle, -1);// -1 is the driver seat

            // spawn shooters
            shooters[0] = new Ped("a_f_y_bevhills_03", this.CalloutPosition, 0f);
            if (!shooters[0]) return false;
            shooters[0].Armor = 100;
            shooters[0].WarpIntoVehicle(vehicle, 1);// 1 is the rear left seat
            shooters[0].Inventory.GiveNewWeapon("WEAPON_MICROSMG", -1, true);
            shooters[0].Accuracy = 1;

            shooters[1] = new Ped("a_m_y_smartcaspat_01", this.CalloutPosition, 0f);
            if (!shooters[1]) return false;
            shooters[1].Armor = 100;
            shooters[1].WarpIntoVehicle(vehicle, 2);// 2 is the rear right seat
            shooters[1].Inventory.GiveNewWeapon("WEAPON_MICROSMG", -1, true);
            shooters[1].Accuracy = 1;

            return true;
        }
        
        //private void VisualLostHandler(LHandle pursuitHandle, Ped ped, bool hasVisual, bool justChanged)
        //{
        //    if (ped.Equals(driver))
        //    {
        //        Game.DisplaySubtitle("[BabyDriver] IsPedVisualLostLonger: " + Functions.IsPedVisualLostLonger(driver));
        //        //Game.LogTrivial("[BabyDriver] IsPedVisualLost: " + Functions.IsPedVisualLost(driver));
        //        //Game.DisplaySubtitle("[BabyDriver] OnPursuitPedHasVisualChanged: hasVisual: " + hasVisual + ", justChanged: " + justChanged, 5000);
        //    }
        //}

        void StartPursuit()
        {
            //Events.OnPursuitPedHasVisualChanged += VisualLostHandler;
            if (suspectBlip) suspectBlip.Delete();

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
            shooters[0].RelationshipGroup.SetRelationshipWith(Player.RelationshipGroup, Relationship.Hate);
            shooters[1].RelationshipGroup.SetRelationshipWith(Player.RelationshipGroup, Relationship.Hate);

            shooters[0].Tasks.FightAgainst(Player);
            shooters[1].Tasks.FightAgainst(Player);

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
