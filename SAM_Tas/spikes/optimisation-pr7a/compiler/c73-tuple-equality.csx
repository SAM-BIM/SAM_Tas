// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
var a = (1, 2);
var b = (1, 2);
ScriptOutput.SetValue("Result", a == b ? 1 : 0);
