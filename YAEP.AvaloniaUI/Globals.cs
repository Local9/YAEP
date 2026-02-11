// System namespaces
// Avalonia namespaces
// Note: Avalonia.Controls is not included globally to avoid conflicts with System.Drawing.Image
global using Avalonia;
global using Avalonia.Threading;
// CommunityToolkit.Mvvm namespaces
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Threading.Tasks;
// YAEP namespaces
global using YAEP.Interface;
global using YAEP.Services;
global using YAEP.Shared.Interfaces;

namespace YAEP
{
    /// <summary>
    /// Constants for EVE Online window title detection and display.
    /// </summary>
    public static class EveWindowTitleConstants
    {
        /// <summary>Base window title without character name (e.g. "EVE").</summary>
        public const string EveWindowTitleBase = "EVE";

        /// <summary>Prefix for window title with character name (e.g. "EVE - CharacterName").</summary>
        public const string EveWindowTitlePrefix = "EVE - ";
    }
}
