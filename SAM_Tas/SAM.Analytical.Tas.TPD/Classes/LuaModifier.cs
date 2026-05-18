// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;

namespace SAM.Analytical.Tas.TPD
{
    public class LuaModifier : SimpleModifier
    {
        public string Code { get; set; }

        public LuaModifier(ArithmeticOperator arithmeticOperator, string code)
        {
            ArithmeticOperator = arithmeticOperator;
            Code = code;
        }

        public LuaModifier(LuaModifier luaModifier)
            : base(luaModifier)
        {
            if(luaModifier != null)
            {
                Code = luaModifier.Code;
            }
        }

        public LuaModifier(JsonObject jObject)
            : base(jObject)
        {

        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            bool result = base.FromJsonObject(jObject);
            if (!result)
            {
                return result;
            }

            if (jObject.ContainsKey("Code"))
            {
                Code = jObject["Code"]?.GetValue<string>() ?? null;
            }

            return result;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if (result == null)
            {
                return null;
            }

            if (Code != null)
            {
                result.Add("Code", Code);
            }

            return result;
        }
    }
}
