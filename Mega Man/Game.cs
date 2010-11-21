﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Drawing;
using System.Xml.Linq;
using MegaMan;

namespace Mega_Man
{
    // These args, and the event, are used to forcibly resize
    // the window and screen when the game is loaded, since
    // the game can specify its size in pixels.
    public class ScreenSizeChangedEventArgs : EventArgs
    {
        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }

        public ScreenSizeChangedEventArgs(int across, int down)
        {
            PixelsAcross = across;
            PixelsDown = down;
        }
    }

    public class Game
    {
        public static Game CurrentGame { get; private set; }

        private string currentPath;
        private IHandleGameEvents currentHandler;
        private StageSelect select;
        private PauseScreen pauseScreen;
        private List<IHandleGameEvents> gameObjects;
        private Dictionary<string, FilePath> stages;

        public MapHandler CurrentMap { get; private set; }
        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }
        public float Gravity { get; private set; }
        public bool GravityFlip { get; set; }

        public bool Paused { get; private set; }

        public string BasePath { get; private set; }

        public static event EventHandler<ScreenSizeChangedEventArgs> ScreenSizeChanged;

        public int PlayerLives { get; set; }

        private Font font;

        public static void Load(string path)
        {
            Engine.Instance.Start();
            if (CurrentGame != null)
            {
                CurrentGame.Unload();
            }
            CurrentGame = new Game();
            CurrentGame.LoadFile(path);
        }

        private void StopHandlers()
        {
            List<IHandleGameEvents> temp = new List<IHandleGameEvents>(gameObjects);
            foreach (IHandleGameEvents handler in temp) handler.StopHandler();
        }

        public void Unload()
        {
            Engine.Instance.Stop();
            StopHandlers();
            GameEntity.UnloadAll();
            select = null;
            Engine.Instance.UnloadAudio();
            FontSystem.Unload();
            CurrentGame = null;
        }

        public void Reset()
        {
            Unload();
            Load(currentPath);
        }

        private Game()
        {
            gameObjects = new List<IHandleGameEvents>();
            stages = new Dictionary<string, FilePath>();
            font = new Font(FontFamily.GenericMonospace, 14, FontStyle.Bold);

            Gravity = 0.25f;
            GravityFlip = false;

            PlayerLives = 2;
        }

        private void LoadFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The project file does not exist: " + path);

            BasePath = System.IO.Path.GetDirectoryName(path);
            XElement reader = XElement.Load(path);

            XElement sizeNode = reader.Element("Size");
            if (sizeNode != null)
            {
                int across, down;
                if (!int.TryParse(sizeNode.Attribute("x").Value, out across)) throw new EntityXmlException(path, (sizeNode as IXmlLineInfo).LineNumber, null, "Size", "x", "Specified width was not a valid integer.");
                if (!int.TryParse(sizeNode.Attribute("y").Value, out down)) throw new EntityXmlException(path, (sizeNode as IXmlLineInfo).LineNumber, null, "Size", "y", "Specified height was not a valid integer.");
                PixelsDown = down;
                PixelsAcross = across;
                if (ScreenSizeChanged != null)
                {
                    ScreenSizeChangedEventArgs args = new ScreenSizeChangedEventArgs(PixelsAcross, PixelsDown);
                    ScreenSizeChanged(this, args);
                }
            }

            XElement stagesNode = reader.Element("Stages");
            if (stagesNode != null)
            {
                foreach (var node in stagesNode.Elements("Stage"))
                {
                    var nameNode = node.Attribute("name");
                    var pathNode = node.Attribute("path");
                    if (nameNode == null || pathNode == null) continue;

                    stages.Add(nameNode.Value, FilePath.FromRelative(pathNode.Value, this.BasePath));
                }
            }

            XElement stageSelectNode = reader.Element("StageSelect");
            if (stageSelectNode != null) select = new StageSelect(stageSelectNode);

            XElement pauseNode = reader.Element("PauseScreen");
            if (pauseNode != null) pauseScreen = new PauseScreen(pauseNode);

            if (pauseScreen != null) pauseScreen.Unpaused += new Action(pauseScreen_Unpaused);

            foreach (XElement entityNode in reader.Elements("Entities"))
            {
                string enemyfile = System.IO.Path.Combine(BasePath, entityNode.Value);
                GameEntity.LoadEntities(enemyfile);
            }
            currentPath = path;

            StageSelect();
        }

        private void select_MapSelected(string name)
        {
            if (!this.stages.ContainsKey(name)) return;

            select.MapSelected -= select_MapSelected;
            currentHandler.StopHandler();
            CurrentMap = new MapHandler(new MegaMan.Map(this.stages[name]));
            currentHandler = CurrentMap;
            CurrentMap.StartHandler();
            CurrentMap.Paused += CurrentMap_Paused;
            CurrentMap.End += CurrentMap_End;
            CurrentMap.Player.Death += () => { PlayerLives--; };
        }

        // do this when a map is won - should change to get weapon screen
        void CurrentMap_End()
        {
            EndMap();
            StageSelect();
        }

        void CurrentMap_Paused()
        {
            Game.CurrentGame.Pause();
            if (pauseScreen != null) pauseScreen.Sound();
            Engine.Instance.FadeTransition(PauseScreen);
        }

        private void EndMap()
        {
            // includes the map
            StopHandlers();
            GameEntity.StopAll();
            CurrentMap.Paused -= CurrentMap_Paused;
            CurrentMap.End -= CurrentMap_End;
            CurrentMap = null;
        }

        private void StageSelect()
        {
            currentHandler = select;
            select.MapSelected += select_MapSelected;
            select.StartHandler();
        }

        private void PauseScreen()
        {
            if (pauseScreen != null)
            {
                CurrentMap.Pause();
                pauseScreen.StartHandler();
            }
        }

        void pauseScreen_Unpaused()
        {
            if (pauseScreen != null) pauseScreen.Sound();
            Engine.Instance.FadeTransition(UnPause, Game.CurrentGame.Unpause);
        }

        public void UnPause()
        {
            CurrentMap.Unpause();
            if (pauseScreen != null)
            {
                pauseScreen.ApplyWeapon();
                pauseScreen.StopHandler();
            }
        }

        public void ResetMap()
        {
            if (CurrentMap == null) return;
            StopHandlers();
            GameEntity.StopAll();
            CurrentMap.StopHandler();

            if (PlayerLives < 0) // game over!
            {
                EndMap();
                StageSelect();
                PlayerLives = 2;
            }
            else
            {
                CurrentMap.StartHandler();
                CurrentMap.Player.Death += () => { PlayerLives--; };
            }
        }

        public void AddGameHandler(IHandleGameEvents handler)
        {
            gameObjects.Add(handler);
        }

        public void RemoveGameHandler(IHandleGameEvents handler)
        {
            gameObjects.Remove(handler);
        }

        public void Pause()
        {
            Paused = true;
        }

        public void Unpause()
        {
            Paused = false;
        }
    }
}
