﻿// Copyright (c) 2014 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Linq;

using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.Decompiler.CSharp.Transforms
{
	/// <summary>
	/// Escape invalid identifiers.
	/// </summary>
	/// <remarks>
	/// This transform is not enabled by default.
	/// </remarks>
	public class EscapeInvalidIdentifiers : IAstTransform
	{
		bool IsValid(char ch)
		{
			if (char.IsLetterOrDigit(ch))
				return true;
			if (ch == '_')
				return true;
			return false;
		}

		string ReplaceInvalid(string s)
		{
			string name = string.Concat(s.Select(ch => IsValid(ch) ? ch.ToString() : string.Format("_{0:X4}", (int)ch)));
			if (name.Length >= 1 && !(char.IsLetter(name[0]) || name[0] == '_'))
				name = "_" + name;
			return name;
		}

		public void Run(AstNode rootNode, TransformContext context)
		{
			foreach (var ident in rootNode.DescendantsAndSelf.OfType<Identifier>())
			{
				ident.Name = ReplaceInvalid(ident.Name);
			}
		}
	}

	/// <summary>
	/// This transform is used to remove assembly-attributes that are generated by the compiler,
	/// thus don't need to be declared. (We have to remove them, in order to avoid conflicts while compiling.)
	/// </summary>
	/// <remarks>This transform is only enabled, when exporting a full assembly as project.</remarks>
	public class RemoveCompilerGeneratedAssemblyAttributes : IAstTransform
	{
		public void Run(AstNode rootNode, TransformContext context)
		{
			foreach (var section in rootNode.Children.OfType<AttributeSection>())
			{
				if (section.AttributeTarget == "assembly")
				{
					foreach (var attribute in section.Attributes)
					{
						var trr = attribute.Type.Annotation<TypeResolveResult>();
						if (trr == null)
							continue;

						string fullName = trr.Type.FullName;
						var arguments = attribute.Arguments;
						switch (fullName)
						{
							case "System.Diagnostics.DebuggableAttribute":
							{
								attribute.Remove();
								break;
							}
							case "System.Runtime.CompilerServices.CompilationRelaxationsAttribute":
							{
								if (arguments.Count == 1 && arguments.First() is PrimitiveExpression expr && expr.Value is int value && value == 8)
									attribute.Remove();
								break;
							}
							case "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute":
							{
								if (arguments.Count != 1)
									break;
								if (!(arguments.First() is NamedExpression expr1) || expr1.Name != "WrapNonExceptionThrows")
									break;
								if (!(expr1.Expression is PrimitiveExpression expr2) || !(expr2.Value is bool value) || value != true)
									break;
								attribute.Remove();
								break;
							}
							case "System.Runtime.Versioning.TargetFrameworkAttribute":
							{
								attribute.Remove();
								break;
							}
							case "System.Security.Permissions.SecurityPermissionAttribute":
							{
								if (arguments.Count != 2)
									break;
								if (!(arguments.First() is MemberReferenceExpression expr1) || expr1.MemberName != "RequestMinimum")
									break;
								if (!(expr1.NextSibling is NamedExpression expr2) || expr2.Name != "SkipVerification")
									break;
								if (!(expr2.Expression is PrimitiveExpression expr3) || !(expr3.Value is bool value2) || value2 != true)
									break;
								attribute.Remove();
								break;
							}
						}
					}
				}
				else if (section.AttributeTarget == "module")
				{
					foreach (var attribute in section.Attributes)
					{
						var trr = attribute.Type.Annotation<TypeResolveResult>();
						if (trr == null)
							continue;

						switch (trr.Type.FullName)
						{
							case "System.Security.UnverifiableCodeAttribute":
								attribute.Remove();
								break;
						}
					}
				}
				else
				{
					continue;
				}

				if (section.Attributes.Count == 0)
				{
					section.Remove();
				}
			}
		}
	}

	/// <summary>
	/// This transform is used to remove attributes that are embedded 
	/// </summary>
	public class RemoveEmbeddedAttributes : DepthFirstAstVisitor, IAstTransform
	{
		internal static readonly HashSet<string> attributeNames = new HashSet<string>() {
			"System.Runtime.CompilerServices.IsReadOnlyAttribute",
			"System.Runtime.CompilerServices.IsByRefLikeAttribute",
			"System.Runtime.CompilerServices.IsUnmanagedAttribute",
			"System.Runtime.CompilerServices.NullableAttribute",
			"System.Runtime.CompilerServices.NullableContextAttribute",
			"System.Runtime.CompilerServices.NativeIntegerAttribute",
			"System.Runtime.CompilerServices.RefSafetyRulesAttribute",
			"System.Runtime.CompilerServices.ScopedRefAttribute",
			"System.Runtime.CompilerServices.RequiresLocationAttribute",
			"Microsoft.CodeAnalysis.EmbeddedAttribute",
		};

		public override void VisitTypeDeclaration(TypeDeclaration typeDeclaration)
		{
			var typeDefinition = typeDeclaration.GetSymbol() as ITypeDefinition;
			if (typeDefinition == null || !attributeNames.Contains(typeDefinition.FullName))
				return;
			if (!typeDefinition.HasAttribute(KnownAttribute.Embedded))
				return;
			if (typeDeclaration.Parent is NamespaceDeclaration ns && ns.Members.Count == 1)
				ns.Remove();
			else
				typeDeclaration.Remove();
		}

		public void Run(AstNode rootNode, TransformContext context)
		{
			rootNode.AcceptVisitor(this);
		}
	}

}
