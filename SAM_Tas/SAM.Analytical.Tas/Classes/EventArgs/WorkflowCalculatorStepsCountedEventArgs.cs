// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public class WorkflowCalculatorStepsCountedEventArgs
    {
        public int Count { get; }

        public WorkflowCalculatorStepsCountedEventArgs(int count)
        {
            Count = count;
        }
    }
}
