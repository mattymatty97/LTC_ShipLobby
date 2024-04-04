using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace LobbyControl.PopUp
{
    
    [HarmonyPatch]
    public class PopUpPatch
    {
        public static readonly List<Tuple<string, string>> PopUps = new List<Tuple<string, string>>();
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Awake))]
        private static void AddPopups(MenuManager __instance)
        {
            foreach (Tuple<string,string> popup in PopUps)
            {
                AppendPopup(popup.Item1,popup.Item2);
            }
        }
        
        
        private static void AppendPopup(string name, string text)
        {
            
            var menuContainer = GameObject.Find("/Canvas/MenuContainer/");
            var lanPopup = GameObject.Find("Canvas/MenuContainer/LANWarning/");
            if (lanPopup == null) 
                return;
            
            var newPopup = UnityEngine.Object.Instantiate(lanPopup, menuContainer.transform);
            newPopup.name = name;
            newPopup.SetActive(true);
            var textHolder = GameObject.Find($"Canvas/MenuContainer/{name}/Panel/NotificationText");
            var textMesh = textHolder.GetComponent<TextMeshProUGUI>();
            textMesh.text = text;
        }
    }
}