// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
#r "{{SAM_BUILD}}\SAM.Core.dll"
var range = new SAM.Core.Range<double>(1, 4);
ScriptOutput.SetValue("Result", range.Max);
