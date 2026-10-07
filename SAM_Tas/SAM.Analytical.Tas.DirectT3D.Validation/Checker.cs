// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>Collects PASS/FAIL lines so a run can print them, write them, and fail the process on any failure.</summary>
    public sealed class Checker
    {
        private readonly List<string> lines = new List<string>();
        public int Passed { get; private set; }
        public int Failed { get; private set; }

        public void Section(string title)
        {
            Add(string.Empty);
            Add("=== " + title);
        }

        public void Info(string text)
        {
            Add("    " + text);
        }

        public bool True(bool condition, string description)
        {
            if (condition) Passed++; else Failed++;
            Add((condition ? "PASS  " : "FAIL  ") + description);
            return condition;
        }

        public bool Near(double actual, double expected, double tolerance, string description)
        {
            bool ok = Math.Abs(actual - expected) <= tolerance;
            return True(ok, string.Format(CultureInfo.InvariantCulture, "{0}: {1:F4} (expected {2:F4} +/- {3:F4})", description, actual, expected, tolerance));
        }

        public bool Equal<T>(T actual, T expected, string description)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            return True(ok, string.Format(CultureInfo.InvariantCulture, "{0}: {1} (expected {2})", description, actual, expected));
        }

        private void Add(string line)
        {
            lines.Add(line);
            Console.WriteLine(line);
        }

        public void Write(string path)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string line in lines) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "TOTAL passed={0} failed={1}", Passed, Failed));
            File.WriteAllText(path, sb.ToString());
        }
    }
}
