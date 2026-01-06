// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections;
using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    public abstract class GenOptEnumerable<T> : GenOptObject, IEnumerable<T> where T : IGenOptObject
    {
        protected List<T> values = new List<T>();

        public IEnumerator<T> GetEnumerator()
        {
            return values == null ? (new List<T>() ).GetEnumerator(): values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected override string GetText()
        {
            if(values == null)
            {
                return null;
            }

            List<string> texts = new List<string>();
            foreach(T value in values)
            {
                texts.Add(value.Text);
            }


            return string.Join("\n", texts);
        }
    }
}
