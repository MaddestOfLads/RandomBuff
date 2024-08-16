﻿using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;
using UnityEngine;
using static RandomBuffUtils.BuffInput;

namespace RandomBuffUtils
{
    public class BuffInput
    {
        /// <summary>
        /// 获取按键输入
        /// </summary>
        /// <param name="action">通过OnAnyKeyDown获取的keyCode值</param>
        /// <returns></returns>
        public static bool GetKeyDown(string action)
        {
            if (action.StartsWith("Axis"))
            {
                action = action.Replace("Joystick", "");
                var colIndex = action[0];
                if (char.IsDigit(colIndex))
                    action = action.Substring(1);

                action = action.Replace("Axis ", "");
                if (ReInput.controllers?.Joysticks == null)
                    return false;
                else if (char.IsDigit(colIndex) && ReInput.controllers.Joysticks.Count > colIndex - '0')
                    return ReInput.controllers.Joysticks[colIndex - '0'].GetAxisTimeActiveById(action[0] - '0') != 0 &&
                           ReInput.controllers.Joysticks[colIndex - '0'].GetAxisLastTimeActiveById(action[0] - '0') == 0;
                else
                    return ReInput.controllers.Joysticks.Any(col => col.GetAxisTimeActiveById(action[0] - '0') != 0 &&
                                                                    col.GetAxisLastTimeActiveById(action[0] - '0') == 0);
            }
            return Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), action));
        }


        /// <summary>
        /// 获取按键输入
        /// </summary>
        /// <param name="action">通过OnAnyKeyDown获取的keyCode值</param>
        /// <returns></returns>
        public static bool GetKey(string action)
        {
            if (action.Contains("Axis"))
            {
                action = action.Replace("Joystick", "");
                var colIndex = action[0];
                if (char.IsDigit(colIndex))
                    action = action.Substring(1);

                action = action.Replace("Axis ", "");
                if (ReInput.controllers?.Joysticks == null)
                    return false;
                else if (char.IsDigit(colIndex) && ReInput.controllers.Joysticks.Count > colIndex - '0')
                    return ReInput.controllers.Joysticks[colIndex - '0'].GetAxisTimeActiveById(action[0] - '0') != 0;
                else
                    return ReInput.controllers.Joysticks.Any(col => col.GetAxisTimeActiveById(action[0] - '0') != 0);
            }
            return Input.GetKey((KeyCode)Enum.Parse(typeof(KeyCode), action));
        }

        
        public static event BuffEvent.KeyDownHandler OnAnyKeyDown
        {
            add
            {
                if (Listeners.Count == 0)
                    On.ProcessManager.Update += ProcessManager_Update;
                
                if(!Listeners.ContainsKey(value))
                    Listeners.Add(value,new BuffInputListener(value));
            }
            remove
            {
                if (Listeners.ContainsKey(value))
                {
                    Listeners.Remove(value);
                    if (Listeners.Count == 0)
                        On.ProcessManager.Update -= ProcessManager_Update;
                }
            }
        }

        private static void ProcessManager_Update(On.ProcessManager.orig_Update orig, ProcessManager self, float deltaTime)
        {
            orig(self,deltaTime);
            foreach(var listener in Listeners.ToArray())
                listener.Value.ListenInput();
        }

        static readonly Dictionary<BuffEvent.KeyDownHandler,BuffInputListener> Listeners = new ();
    }


    internal class BuffInputListener
    {
        internal BuffInputListener(BuffEvent.KeyDownHandler handler)
        {
            keyDownHandler = handler;
        }
        public void ListenInput()
        {
            foreach (int code in Enum.GetValues(typeof(KeyCode)))
            {
                string name = ((KeyCode)(code)).ToString();
                if (Input.GetKey((KeyCode)code))
                {
                    if (!alreadyDown.Contains(name))
                    {
                        alreadyDown.Add(name);
                        keyDownHandler?.Invoke(name);
                    }
                }
                else
                {
                    if (alreadyDown.Contains(name))
                        alreadyDown.Remove(name);
                }
            }
            foreach (var col in ReInput.controllers.Joysticks)
            {
                foreach (var axis in col.Axes)
                {
                    string name = "Axis " + axis.id;
                    if (axis.timeActive != 0)
                    {
                        if (!alreadyDown.Contains(name))
                        {
                            alreadyDown.Add(name);
                            keyDownHandler?.Invoke(name);
                        }
                    }
                    else
                    {
                        if (alreadyDown.Contains(name))
                            alreadyDown.Remove(name);
                    }
                }
            }
        }

        private readonly HashSet<string> alreadyDown = new HashSet<string>();

        private readonly BuffEvent.KeyDownHandler keyDownHandler;
    }
}
