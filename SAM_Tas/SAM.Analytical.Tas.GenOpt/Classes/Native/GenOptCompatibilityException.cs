// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// A GenOpt configuration the native adapter refuses because GenOpt 3.1.1 itself would not accept it, or because
    /// it is invalid (never silently repaired). The message names the setting and the reason.
    /// </summary>
    public class GenOptCompatibilityException : InvalidOperationException
    {
        public GenOptCompatibilityException(string message)
            : base(message)
        {
        }
    }
}
