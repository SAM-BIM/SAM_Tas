// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
var w = System.Diagnostics.Stopwatch.StartNew();
object o = new object();
bool com = System.Runtime.InteropServices.Marshal.IsComObject(o);
ScriptOutput.SetValue("Result", com ? 0 : 1);
