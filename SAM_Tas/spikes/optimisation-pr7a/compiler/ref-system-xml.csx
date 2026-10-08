// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
var doc = new System.Xml.XmlDocument();
doc.LoadXml("<a b=\"4\"/>");
ScriptOutput.SetValue("Result", double.Parse(doc.DocumentElement.GetAttribute("b")));
