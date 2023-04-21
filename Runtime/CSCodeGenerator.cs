﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Megumin
{
    /// <summary>
    /// 代码生成器
    /// </summary>
    public abstract class CodeGenerator
    {
        [Serializable]
        public class Mecro
        {
            public string Name;
            public string Value;
        }

        /// <summary>
        /// 允许一个宏嵌套另一个宏
        /// </summary>
        public List<Mecro> MecroList { get; set; }
        /// <summary>
        /// 允许一个宏嵌套另一个宏
        /// </summary>
        public Dictionary<string, string> Macro = new Dictionary<string, string>();

        /// <summary>
        /// 新行换行符，默认使用 "\n"
        /// </summary>
        public string NewLine = "\n";

        //TODO
        public static Dictionary<string, string> ProjectMacro = new Dictionary<string, string>();

        /// <summary>
        /// 递归替换宏。
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string ReplaceMacro(string code)
        {
            StringBuilder sb = new StringBuilder(code);

            if (MecroList != null)
            {
                foreach (var item in MecroList)
                {
                    sb = sb.Replace(item.Name, item.Value);
                }
            }

            foreach (var item in Macro)
            {
                sb = sb.Replace(item.Key, item.Value);
            }

            var result = sb.ToString();
            if (ContainsMacro(result))
            {
                result = ReplaceMacro(result);
            }

            return result;
        }

        public bool ContainsMacro(string code)
        {
            if (MecroList != null)
            {
                foreach (var item in MecroList)
                {
                    if (code.Contains(item.Name))
                    {
                        return true;
                    }
                }
            }

            foreach (var item in Macro)
            {
                if (code.Contains(item.Key))
                {
                    return true;
                }
            }

            return false;
        }

        public abstract string GetCodeString();

        public virtual void Generate(string path, Encoding encoding = null)
        {
            string txt = GetCodeString();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"///********************************************************************************************************************************{NewLine}");
            stringBuilder.Append($"///本页代码由代码生成器生成，请勿手动修改。The code on this page is generated by the code generator, do not manually modify.{NewLine}");
            stringBuilder.Append($"///生成器类型：$(CodeGenericType){NewLine}");
            stringBuilder.Append($"///配置源文件：$(CodeGenericSourceFilePath){NewLine}");
            stringBuilder.Append($"///********************************************************************************************************************************{NewLine}");
            stringBuilder.Append(NewLine);
            stringBuilder.Append(txt);
            txt = stringBuilder.ToString();

            txt = ReplaceMacro(txt);

            if (encoding == null)
            {
                encoding = new System.Text.UTF8Encoding(true);
            }

            File.WriteAllText(path, txt, encoding);
        }

        public virtual void GenerateNear(UnityEngine.Object near, string replaceFileName = null, Encoding encoding = null)
        {
#if UNITY_EDITOR

            if (!Macro.ContainsKey("$(ClassName)"))
            {
                Macro["$(ClassName)"] = near.name;
            }

            var path = UnityEditor.AssetDatabase.GetAssetPath(near);
            var projectPath = Path.Combine(Application.dataPath, $"..\\");
            var gp = Path.Combine(projectPath, path);
            gp = Path.GetFullPath(gp);

            var finfo = new FileInfo(gp);

            Macro["$(CodeGenericType)"] = near.GetType().Name;
            Macro["$(CodeGenericSourceFilePath)"] = finfo.ToString();

            string fileName = $"{near.name}_GenericCode.cs";
            if (!string.IsNullOrEmpty(replaceFileName))
            {
                fileName = $"{replaceFileName}.cs";
            }

            var fn = Path.Combine(finfo.Directory.ToString(), fileName);
            fn = Path.GetFullPath(fn);

            Generate(fn, encoding);

            UnityEditor.AssetDatabase.Refresh();

#else
            throw new NotSupportedException();
#endif

        }

        public string UpperStartChar(string str, int length = 1)
        {
            if (str.Length >= 1)
            {
                var up = str.Substring(0, length).ToUpper() + str.Substring(length);
                return up;
            }

            return str;
        }
    }

    /// <summary>
    /// 代码生成器
    /// </summary>
    public class CSCodeGenerator : CodeGenerator
    {
        public static string GetIndentStr(int level = 1)
        {
            string res = "";
            for (int i = 0; i < level; i++)
            {
                res += "    ";
            }
            return res;
        }

        public int Indent { get; internal set; } = 0;
        public List<string> Lines { get; set; } = new List<string>();

        /// <summary>
        /// 添加一个空行
        /// </summary>
        /// <param name="count"></param>
        public void PushBlankLines(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Push("");
            }
        }

        public void Push(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                code = GetIndentStr(Indent) + code;
            }

            Lines.Add(code);
        }

        public void Push(CSCodeGenerator generator)
        {
            foreach (var item in generator.Lines)
            {
                Push(item);
            }
        }

        /// <summary>
        /// 添加模板代码或者多行代码。
        /// 内部根据<see cref="Environment.NewLine"/>,拆分成单行添加到生成器中。
        /// </summary>
        /// <param name="template"></param>
        public void PushTemplate(string template)
        {
            var newLine = Environment.NewLine;
            PushTemplate(template, newLine);
        }

        /// <summary>
        /// 添加模板代码或者多行代码。
        /// 内部根据指定 换行符拆,拆分成单行添加到生成器中。
        /// </summary>
        /// <param name="template"></param>
        public void PushTemplate(string template, string LF)
        {
            var lines = template.Split(LF);
            foreach (var line in lines)
            {
                Push(line);
            }
        }

        public void PushComment(string comment)
        {
            if (comment == null || comment.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(comment))
            {
                return;
            }

            //增加注释
            //Push("");  //不要自动添加空行
            Push(@$"/// <summary>");
            Push(@$"/// {comment}");
            Push(@$"/// </summary>");
        }

        public void PushComment(params string[] comments)
        {
            if (comments == null || comments.Length == 0)
            {
                return;
            }

            if (comments.Length == 1 && string.IsNullOrEmpty(comments[0]))
            {
                return;
            }

            //增加注释
            //Push("");  //不要自动添加空行
            Push(@$"/// <summary>");
            foreach (var item in comments)
            {
                StringReader sr = new StringReader(item);
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    Push(@$"/// <para/> {line}");
                }
            }
            Push(@$"/// </summary>");
        }

        public void BeginScope()
        {
            Push(@$"{{");
            Indent++;
        }

        public void EndScope()
        {
            Indent--;
            Push(@$"}}");
        }

        /// <summary>
        /// 结束一个区域,附加一个字符，通常用于 "," 或者 ";"
        /// </summary>
        public void EndScopeWith(string str)
        {
            Indent--;
            Push(@$"}}{str}");
        }

        /// <summary>
        /// 结束一个区域并附带 ";"
        /// </summary>
        public void EndScopeWithSemicolon()
        {
            Indent--;
            Push(@$"}};");
        }

        /// <summary>
        /// 结束一个区域并附带 ","
        /// </summary>
        public void EndScopeWithComma()
        {
            Indent--;
            Push(@$"}},");
        }

        /// <summary>
        /// TODO, 使用sb，换行符问题
        /// </summary>
        /// <returns></returns>
        public override string GetCodeString()
        {
            //string txt = "";
            StringBuilder stringBuilder = new();
            foreach (var item in Lines)
            {
                //txt += item + NewLine;
                stringBuilder.Append(item);
                stringBuilder.Append(NewLine);
            }

            var result = stringBuilder.ToString();

            //var v = txt == result;
            return result;
        }

        public class Scope : IDisposable
        {
            CSCodeGenerator g;
            public string EndWith { get; set; }
            public Scope(CSCodeGenerator g)
            {
                this.g = g;
                g.BeginScope();
            }
            public void Dispose()
            {
                g.EndScopeWith(EndWith);
            }
        }

        /// <summary>
        /// 使用using
        /// </summary>
        /// <returns></returns>
        public IDisposable EnterScope()
        {
            return new Scope(this);
        }

        public IDisposable NewScope
        {
            get
            {
                return new Scope(this);
            }
        }

        public Scope GetNewScope(string endWith = null)
        {
            return new Scope(this) { EndWith = endWith };
        }
    }

    /// <summary>
    /// 简单的模板代码生成器。没有循环结构。
    /// </summary>
    public class TemplateCodeGenerator : CodeGenerator
    {
        public TextAsset Template;

        public override string GetCodeString()
        {
            return Template.text;
        }
    }

    public static class CodeGeneratorExtension_B2F1FF890B2949E1B0431530F1D90322
    {
        public static void Test()
        {
            string result = "";
            result = typeof(List<int>).ToCodeString();
            result = typeof(int[]).ToCodeString();

            result.ToString();
        }

        public static string ToCodeString(this Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }
            else if (type == typeof(bool))
            {
                return "bool";
            }
            else if (type == typeof(string))
            {
                return "string";
            }
            else if (type == typeof(int))
            {
                return "int";
            }
            else if (type == typeof(long))
            {
                return "long";
            }
            else if (type == typeof(float))
            {
                return "float";
            }
            else if (type == typeof(double))
            {
                return "double";
            }
            else if (type == typeof(decimal))
            {
                return "decimal";
            }

            string result = "";
            if (type.IsGenericType)
            {
                StringBuilder sb = new();
                Type gd = type.GetGenericTypeDefinition();
                var gdFullName = gd.FullName;
                gdFullName = gdFullName[..^2];
                sb.Append(gdFullName);
                sb.Append('<');
                var g = type.GetGenericArguments();
                for (int i = 0; i < g.Length; i++)
                {
                    if (i != 0)
                    {
                        sb.Append(',');
                    }
                    var gType = g[i];
                    sb.Append(gType.ToCodeString());
                }
                sb.Append('>');
                result = sb.ToString();
            }
            else
            {
                result = type.FullName;
            }

            result = result.Replace("+", ".");
            return result;
        }

        /// <summary>
        /// 类型名转为合法变量名或者合法类名
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string ToValidVariableName(this Type type)
        {
            string result = type.Name;
            if (type.IsGenericType)
            {
                var d = type.GetGenericTypeDefinition();
                result = d.Name;
                var gs = type.GetGenericArguments();
                foreach (var g in gs)
                {
                    result += $"_{g.ToValidVariableName()}";
                }
            }

            result = result.TrimEnd('&');
            result = result.Replace("[]", "Array");
            result = result.Replace(".", "_");
            result = result.Replace("`", "_");
            return result;
        }

        public static string UpperStartChar(this string str, int length = 1)
        {
            if (str.Length <= length)
            {
                return str.ToUpper();
            }
            else if (str.Length > length)
            {
                var up = str[..length].ToUpper() + str[length..];
                return up;
            }

            return str;
        }
    }
}


