// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
string s = null;
s ??= "x";
int k = s switch { "x" => 3, _ => 0 };
ScriptOutput.SetValue("Result", k);
