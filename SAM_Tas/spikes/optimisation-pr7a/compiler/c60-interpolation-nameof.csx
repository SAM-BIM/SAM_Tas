// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
// PR7a compiler probe (TasGenExecute script, not compiled by any project).
string name = nameof(ScriptOutput);
string text = $"{name}:{1.5}";
int? n = text?.Length;
ScriptOutput.SetValue("Result", n.Value);
