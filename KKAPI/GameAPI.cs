﻿using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using Manager;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KKAPI
{
    [BepInPlugin(GUID, "Modding API for Koikatsu!", Version)]
    public class GameAPI : BaseUnityPlugin
    {
        internal const string Version = "1.4";
        public const string GUID = "marco.kkapi";

        internal static GameAPI Instance { get; private set; }

        public GameAPI()
        {
            Instance = this;
        }

        private void Start()
        {
            var insideStudio = Application.productName == "CharaStudio";

            MakerAPI.Init(insideStudio);
            StudioAPI.Init(insideStudio);
            CharacterApi.Init();
        }

        public static GameMode GetCurrentGameMode()
        {
            if (StudioAPI.InsideStudio) return GameMode.Studio;
            if (MakerAPI.InsideMaker) return GameMode.Maker;
            if (Game.Instance != null) return GameMode.MainGame;
            return GameMode.Unknown;
        }

        /// <summary>
        /// Check if a plugin is loaded and has at least the minimum version. 
        /// If the plugin is missing or older than minimumVersion, user is shown an error message on screen and false is returned.
        /// Run from Awake or Start, not from constructor!
        /// </summary>
        /// <param name="origin">Your plugin</param>
        /// <param name="guid">Guid of the plugin your plugin is dependant on</param>
        /// <param name="minimumVersion">Minimum version of the required plugin</param>
        /// <returns>True if plugin exists and it's version equals or is newer than minimumVersion, otherwise false</returns>
        public static bool CheckRequiredPlugin(BaseUnityPlugin origin, string guid, Version minimumVersion)
        {
            var target = BepInEx.Bootstrap.Chainloader.Plugins
                .Select(MetadataHelper.GetMetadata)
                .FirstOrDefault(x => x.GUID == guid);
            if (target == null)
            {
                Logger.Log(LogLevel.Message | LogLevel.Error, $"ERROR: Plugin \"{guid}\" required by \"{MetadataHelper.GetMetadata(origin).Name}\" was not found!");
                return false;
            }
            if (minimumVersion > target.Version)
            {
                Logger.Log(LogLevel.Message | LogLevel.Error, $"ERROR: Plugin \"{guid}\" required by \"{MetadataHelper.GetMetadata(origin).Name}\" is outdated! At least v{minimumVersion} is needed!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Check if a plugin that is not compatible with your plugin is loaded. 
        /// If the plugin is loaded, user is shown an error message on screen and true is returned.
        /// Run from Awake or Start, not from constructor!
        /// </summary>
        /// <param name="origin">Your plugin</param>
        /// <param name="guid">Guid of the plugin your plugin is incompatible with</param>
        /// <returns>True if plugin exists, otherwise false</returns>
        public static bool CheckIncompatiblePlugin(BaseUnityPlugin origin, string guid)
        {
            var target = BepInEx.Bootstrap.Chainloader.Plugins
                .Select(MetadataHelper.GetMetadata)
                .FirstOrDefault(x => x.GUID == guid);
            if (target != null)
            {
                Logger.Log(LogLevel.Message | LogLevel.Error, $"ERROR: Plugin \"{guid}\" is incompatible with \"{MetadataHelper.GetMetadata(origin).Name}\" and can cause issues!");
                return true;
            }
            return false;
        }

        #region Synchronization

        private readonly object _invokeLock = new object();
        private Action _invokeList;

        /// <summary>
        /// Invoke the Action on the main unity thread. Use to synchronize your threads.
        /// </summary>
        public void SynchronizedInvoke(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            lock (_invokeLock) _invokeList += callback;
        }

        private void Update()
        {
            // Safe to do outside of lock because nothing can remove callbacks, at worst we execute with 1 frame delay
            if (_invokeList == null) return;

            Action toRun;
            lock (_invokeLock)
            {
                toRun = _invokeList;
                _invokeList = null;
            }

            // Need to execute outside of the lock in case the callback itself calls Invoke we could deadlock
            // The invocation would also block any threads that call Invoke
            toRun();
        }

        #endregion
    }
}
