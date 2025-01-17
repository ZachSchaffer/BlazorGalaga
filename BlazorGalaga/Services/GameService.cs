﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Blazor.Extensions.Canvas.WebGL;
using BlazorGalaga.Interfaces;
using BlazorGalaga.Models;
using BlazorGalaga.Models.Paths;
using BlazorGalaga.Models.Paths.Challenges.Challenge3;
using BlazorGalaga.Models.Paths.Intros;
using BlazorGalaga.Static;
using BlazorGalaga.Static.GameServiceHelpers;
using BlazorGalaga.Static.Levels;
using BlazorGalaganimatable.Models.Paths;
using static BlazorGalaga.Pages.Index;

namespace BlazorGalaga.Services
{
    public partial class GameService
    {
        #region Vars

        public AnimationService animationService { get; set; }
        public SpriteService spriteService { get; set; }
        public Ship Ship { get; set; }
        public int Lives { get; set; }
        public int Level { get; set; }
        public int Score { get; set; }
        public bool Started { get; set; }
        public int HighScore { get; set; }

        private int prevbugcount;
        private bool capturehappened;
        private int hits;
        private int wave;
        private bool hideintroscreen;
        private bool introsounddone;
        private int divespeedincrease;
        private int missileincrease;
        private int introspeedincrease;
        private int maxwaittimebetweendives;
        private bool canmorph;
        private bool gameover;
        private float LastDiveTimeStamp;
        private int NextDiveWaitTime;
        private int LevelOffset = 0;
        private int nextextralifescore;

        //for debugging
        public bool debugmode = false;
        private bool skipintro = false;
        private bool soundoff = false;
        private bool aion = true;
        private bool shipinvincable = false;
        private bool showdebugdetails = false;
        private bool infinitelives = false;

        #endregion

        #region Init

        public void Init()
        {
            InitVars();
            //Level = 2;
            ShipManager.InitShip(animationService);
            SoundManager.OnEnd += SoundManager_OnEnd; 
        }

        private void InitVars()
        {
            prevbugcount = 0;
            capturehappened = false;
            hits = 0;
            wave = 1;
            hideintroscreen = false;
            introsounddone = false;
            divespeedincrease = 0;
            missileincrease = 0;
            introspeedincrease = 0;
            maxwaittimebetweendives = 5000;
            canmorph = true;
            gameover = false;
            Started = false;
            Lives = 2;
            Level = 0;
            Score = 0;
            nextextralifescore = Constants.ExtraLifeIncrement;
            LastDiveTimeStamp = 0;
            NextDiveWaitTime = 0;
            if(Ship != null)
                Ship.Sprite = new Sprite(Sprite.SpriteTypes.Ship);
            if (HighScore == 0) HighScore = Constants.MinHighScore;
        }

        private void InitLevel(int level)
        {
            switch (level)
            {
                case 1:
                    Level1.InitIntro(animationService, introspeedincrease);
                    canmorph = false;
                    break;
                case 2:
                    Level2.InitIntro(animationService, introspeedincrease);
                    canmorph = false;
                    break;
                case 3: //challenge
                    Level3.InitIntro(animationService, -2);
                    break;
                case 4:
                    Level4.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 4000;
                    canmorph = true;
                    break;
                case 5:
                    Level5.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 3000;
                    divespeedincrease = 1;
                    missileincrease = 1;
                    canmorph = true;
                    break;
                case 6:
                    Level6.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 2500;
                    divespeedincrease = 1;
                    missileincrease = 1;
                    introspeedincrease = 1;
                    canmorph = true;
                    break;
                case 7: //challenge
                    Level8.InitIntro(animationService, -2);
                    break;
                case 8:
                    Level7.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 2500;
                    divespeedincrease = 1;
                    missileincrease = 2;
                    introspeedincrease = 1;
                    canmorph = true;
                    break;
                case 9:
                    Level9.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 2000;
                    divespeedincrease = 2;
                    missileincrease = 2;
                    introspeedincrease = 1;
                    canmorph = true;
                    break;
                case 10:
                    Level10.InitIntro(animationService, introspeedincrease);
                    maxwaittimebetweendives = 1500;
                    divespeedincrease = 1;
                    missileincrease = 3;
                    introspeedincrease = 2;
                    canmorph = false;
                    break;
                case 11: //challenge
                    Level11.InitIntro(animationService, -3);
                    break;
            }

            GetBugs().ForEach(a => {
                a.Paths.ForEach(p => {
                    if (p.IsIntroPath)
                        a.PathPoints.AddRange(animationService.ComputePathPoints(p, false, 20));
                    else
                        a.PathPoints.AddRange(animationService.ComputePathPoints(p,false));
                });
            });

            ////draw path logic for debugging only
            //var drawpathbug1 = GetBugs().FirstOrDefault(a => a.Intro is Challenge3);
            //var drawpathbug2 = GetBugs().FirstOrDefault(a => a.Intro is Challenge4);

            //drawpathbug1.DrawPath = true;
            //drawpathbug1.DrawControlLines = true;

            //drawpathbug2.DrawPath = true;
            //drawpathbug2.DrawControlLines = true;

            //drawpathbug2.Paths.ForEach(a =>
            //{
            //    a.OutPutDebug = true;
            //    a.DrawPath = true;
            //});
            ////end draw path debugging logic

            GetBugs().Where(a => a.Wave == 1).ToList().ForEach(a => a.Started = true);

            AIManager.aidodgeing = false;
          
        }

        #endregion

        private void Dive()
        {
            if (IsChallengeLevel()) return;

            if (GetBugs().Count == 0 || Ship.Disabled || gameover || Ship.HasExploded || Ship.IsExploding)
                return;

            var bug = EnemyDiveManager.DoEnemyDive(
                GetBugs(),
                animationService,
                Ship,
                Constants.BugDiveSpeed + divespeedincrease,
                null,
                false,
                capturehappened,
                null,
                GetBugs().Any(a=>a.IsMorphedBug) ? false : canmorph);

            if (bug != null && bug.CaptureState == Bug.enCaptureState.Started) capturehappened = true;

            foreach (var b in GetBugs())
            {
                if (b.IsDiving && b.CaptureState == Bug.enCaptureState.NotStarted && !b.IsExploding && b.MissileCountDowns.Count==0)
                {
                    var maxmissleperbug = Utils.Rnd(0,  missileincrease + 1);
                    for (int i = 1; i <= maxmissleperbug; i++)
                    {
                        b.MissileCountDowns.Add(Utils.Rnd(4, 10));
                    }
                }
            }
        }

        private List<Bug> GetBugs()
        {
            return animationService.Animatables.Where(a =>
                a as Bug !=null
            ).Select(a=> a as Bug).ToList();
        }

        private bool IsChallengeLevel()
        {
            return (Level == 3 || Level == 7 || Level == 11);
        }

        public async void Process(float timestamp, GameLoopObject glo)
        {
            if (skipintro)
            {
                skipintro = false;
                hideintroscreen = true;
                introsounddone = true;
                Started = true;
                Ship.Visible = true;
                KeyBoardHelper.SpaceBarPressed = true;
            }

            if (soundoff && !SoundManager.SoundIsOff)
                SoundManager.TurnSoundOff();

            //show the intro screen if the space bar hasn't been pressed yet
            if (!hideintroscreen)
            {
                if (KeyBoardHelper.SpaceBarPressed)
                {
                    SoundManager.PlaySound(SoundManager.SoundManagerSounds.coin, true);
                    await ConsoleManager.ClearConsole(spriteService);
                    await ConsoleManager.DrawConsole(Lives, spriteService, Ship, true, Level + LevelOffset,Score, HighScore);
                    Started = true;
                }
                else
                {
                    await ConsoleManager.ClearConsole(spriteService);
                    await ConsoleManager.DrawIntroScreen(spriteService, Ship);
                    return;
                }
            }

            //if the intro sound isn't done, exit
            if (!introsounddone)
            {
                await ConsoleManager.DrawConsolePlayer1(spriteService);
                return;
            }

            var bugs = GetBugs();

            //do AI if enabled for debugging
            if (aion) AIManager.AI(bugs, animationService, Ship);

            //dive the bugs
            if (timestamp - LastDiveTimeStamp > NextDiveWaitTime && EnemyGridManager.EnemyGridBreathing && !glo.editcurveschecked)
            {
                Dive();
                LastDiveTimeStamp = timestamp;
                NextDiveWaitTime = Utils.Rnd(500, maxwaittimebetweendives);
            }

            if (gameover) {
                Ship.Disabled = true;
            }

            //if the bug intro wave is done, increment to the next wave]
            //or start diving and firing
            if ((bugs.Count(a=>a.Started && !a.IsMoving && a.Wave == wave) > 0 || bugs.Count(a=>a.Wave==wave) == 0) && wave <= 6 && bugs.Count() > 0 && Ship.Visible)
            {
                wave += 1;
                if (wave == 6)
                {
                    EnemyGridManager.EnemyGridBreathing = true;
                    NextDiveWaitTime = Utils.Rnd(500, maxwaittimebetweendives);
                }
                else
                    GetBugs().Where(a => a.Wave == wave).ToList().ForEach(a => a.Started = true);
            }

            //adjust score when bugs are destroyed
            if (bugs.Count != prevbugcount || bugs.Count==0)
            {
                if (Score >= nextextralifescore)
                {
                    Lives += 1;
                    nextextralifescore += 30000;
                    SoundManager.PlaySound(SoundManager.SoundManagerSounds.extralife);
                    await ConsoleManager.ClearConsole(spriteService);
                    await ConsoleManager.DrawConsole(Lives, spriteService, Ship, true, Level + LevelOffset,Score,HighScore);
                }
                if (Score > HighScore) HighScore = Score;
                await ConsoleManager.DrawScore(spriteService, Score, HighScore);
                prevbugcount = bugs.Count();
            }

            //all bugs destroyed, increment to next level
            await DoLevelIncrementAsync(bugs,timestamp);

            //animate explosions
            if (timestamp - EnemyGridManager.LastEnemyGridMoveTimeStamp > 35)
            {
                EnemyExplosionManager.DoEnemyExplosions(bugs, animationService, this);

                if (Ship.IsExploding)
                {
                    if (!Ship.IsDoubleShip)
                        Ship.Disabled = true;
                    ShipManager.DoShipExplosion(Ship, animationService, this);
                }
            }

            //animate child bugs
            ChildBugsManager.MoveChildBugs(bugs, animationService);

            //animated the moving enemy grid
            if (timestamp - EnemyGridManager.LastEnemyGridMoveTimeStamp > 100 || EnemyGridManager.LastEnemyGridMoveTimeStamp == 0)
            {
                EnemyGridManager.MoveEnemyGrid(bugs, animationService, Ship, gameover);
                EnemyGridManager.LastEnemyGridMoveTimeStamp = timestamp;
               
                //fire enemy missiles
                foreach(var bug in bugs.Where(a=>(a.MissileCountDowns.Count > 0 && a.Started) &&
                ((a.IsDiving && a.Location.Y <= Constants.CanvasSize.Height - 400 && a.IsMovingDown && !a.IsMorphedBug) || //for diving bugs
                (a.IsInIntro && a.Wave==wave && a.Location.Y > 100 && a.Location.X > 150 & a.Location.X < Constants.CanvasSize.Width-150 && a.Location.Y <= Constants.CanvasSize.Height - 500)))) //for intro bugs
                {
                    for (int i = 0; i <= bug.MissileCountDowns.Count - 1; i++)
                    {
                        bug.MissileCountDowns[i] -= 1;
                        if (bug.MissileCountDowns[i] <= 0)
                        {
                            EnemyDiveManager.DoEnemyFire(bug, animationService, Ship);
                            bug.MissileCountDowns.RemoveAll(a => a <= 0);
                        }
                    }
                }
            }

            //animate the flapping wings
            if (timestamp - FlapWingsManager.LastWingFlapTimeStamp > 500 || FlapWingsManager.LastWingFlapTimeStamp == 0)
            {
                FlapWingsManager.FlapWings(bugs);
                FlapWingsManager.LastWingFlapTimeStamp = timestamp;
            }

            //animate ship missiles
            if (Ship.IsFiring && !Ship.Disabled && Ship.Visible)
            {
                SoundManager.PlaySound(SoundManager.SoundManagerSounds.fire);
                Ship.IsFiring = false;
                ShipManager.Fire(Ship, animationService);
            }

            //center the ship if it's disabled
            //happens after a galaga capture
            if ((Ship.Disabled && !Ship.IsDoubleShip) || (Ship.HasExploded && !Ship.IsDoubleShip))
            {

                if (Ship.Location.X > 320)
                    Ship.Speed = Constants.ShipMoveSpeed * -1;
                else if (Ship.Location.X < 310)
                    Ship.Speed = Constants.ShipMoveSpeed;
                else
                    Ship.Speed = 0;
            }

            //ship missile detection
            if (!Ship.Disabled)
            {
                //ship mission collision with bug
                hits += ShipManager.CheckMissileCollisions(bugs, animationService);

                //bug or missile collision with ship
                if (!shipinvincable)
                {
                    if (!Ship.IsExploding && Ship.Visible && ShipManager.CheckShipCollisions(bugs, animationService, Ship))
                    {
                        SoundManager.StopAllSounds();
                        Ship.IsExploding = true;
                    }
                }
            }

            //draw fighter captured text if a fighter is captured
            if (bugs.Any(a => a.FighterCapturedMessageShowing))
                await ConsoleManager.DrawConsoleFighterCaptured(spriteService);


            //hide fighter captured text if a fighter is captured
            //and bug had flown back home
            if (bugs.Any(a => a.ClearFighterCapturedMessage))
            {
                await ConsoleManager.ClearConsoleLevelText(spriteService);
                bugs.FirstOrDefault(a => a.ClearFighterCapturedMessage).ClearFighterCapturedMessage = false;
                Lives -= 1;
                if (Lives < 0) gameover = true;
                await ConsoleManager.ClearConsole(spriteService);
                await ConsoleManager.DrawConsole(Lives, spriteService, Ship, true, Level + LevelOffset,Score, HighScore);
            }

            //if morphed bugs go offscreen, destroy them immediately
            bugs.Where(a => a.IsMorphedBug && a.Location.Y >= Constants.CanvasSize.Height).ToList().ForEach(a => a.DestroyImmediately = true);

            //ship exploded
            if (Ship.HasExploded)
            {
                if (Ship.IsDoubleShip)
                {
                    Ship.IsDoubleShip = false;
                    Ship.HasExploded = false;
                    Ship.IsExploding = false;
                    Ship.Visible = true;
                    Ship.Disabled = false;
                    Ship.LeftShipHit = false;
                    Ship.RightShipHit = false;
                    return;
                }
                WaitManager.DoOnce(async () =>
                {
                    if (Lives >= 1)
                    {   //display ready for next life
                        await ConsoleManager.DrawConsoleReady(spriteService);
                        Ship.Disabled = true;
                        Lives -= 1;
                    }
                    else
                    { //game over
                        await ConsoleManager.DrawConsoleGameOver(spriteService);
                        gameover = true;
                        SoundManager.MuteAllSounds = false;
                        SoundManager.PlaySound(SoundManager.SoundManagerSounds.gameoversong, true);
                        Ship.Disabled = true;
                    }
                }, WaitManager.WaitStep.enStep.ShowReady);

                if (WaitManager.WaitFor(3000, timestamp, WaitManager.WaitStep.enStep.WaitReady))
                {
                    if (!animationService.Animatables.Any(a => a.Sprite.SpriteType == Sprite.SpriteTypes.BugMissle) &&
                        !bugs.Any(a=>a.CaptureState == Bug.enCaptureState.Started) && !bugs.Any(a=>a.IsDiving))
                    {
                        Ship.HasExploded = false;
                        Ship.IsExploding = false;
                        if (Lives >= 0)
                        { //load next life
                            Ship.Visible = true;
                            Ship.Disabled = false;
                            await ConsoleManager.ClearConsole(spriteService);
                            await ConsoleManager.DrawConsole(Lives, spriteService, Ship, true, Level + LevelOffset,Score, HighScore);
                            await ConsoleManager.ClearConsoleLevelText(spriteService);
                        }
                        WaitManager.ClearSteps();
                    }
                }
            }

            if (showdebugdetails)
            {
                Utils.dOut("hits", hits);
                Utils.dOut("Ship.IsExploding", Ship.IsExploding);
                Utils.dOut("Ship.HasExploded", Ship.HasExploded);
                Utils.dOut("Ship.Disabled", Ship.Disabled);
                Utils.dOut("gameover", gameover);
                Utils.dOut("Lives", Lives);
                Utils.dOut("Level", Level);
                Utils.dOut("LevelOffset", LevelOffset);
                Utils.dOut("Score", Score);
                Utils.dOut("nextextralifescore", nextextralifescore);
            }

            DebugManager.DoDebugLogic(glo, bugs, animationService,Ship);
        }

        private void SoundManager_OnEnd(Howler.Blazor.Components.Events.HowlEventArgs e)
        {
            var soundname = SoundManager.Sounds.FirstOrDefault(a => a.SoundId == e.SoundId).SoundName;

            if (soundname == SoundManager.SoundManagerSounds.coin)
            {
                hideintroscreen = true;
                SoundManager.PlaySound(SoundManager.SoundManagerSounds.introsong);
            }
            else if (soundname == SoundManager.SoundManagerSounds.introsong)
            {
                introsounddone = true;
            }
            else if (soundname == SoundManager.SoundManagerSounds.gameoversong)
            {
                InitVars();
                KeyBoardHelper.SpaceBarPressed = false;
                GetBugs().ForEach(a => a.DestroyImmediately = true);
            }
        }

    }
}
