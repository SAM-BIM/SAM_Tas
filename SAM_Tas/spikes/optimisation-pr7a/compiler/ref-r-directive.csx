// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
#r "System.Xml.Linq"
var e = System.Xml.Linq.XElement.Parse("<a>6</a>");
ScriptOutput.SetValue("Result", (double)e);
