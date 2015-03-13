﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;

using MoonSharp.Interpreter.Tree.Expressions;

namespace MoonSharp.Interpreter.Tree.Statements
{
	class FunctionDefinitionStatement : Statement
	{
		SymbolRef m_FuncSymbol;
		SourceRef m_SourceRef;

		bool m_Local = false;
		string m_MethodName = null;

		string m_FriendlyName;
		List<string> m_TableAccessors;
		FunctionDefinitionExpression m_FuncDef;

		public FunctionDefinitionStatement(ScriptLoadingContext lcontext, bool local)
			: base(lcontext)
		{
			// here lexer must be at the 'function' keyword
			CheckTokenType(lcontext, TokenType.Function);
			
			m_Local = local;

			if (m_Local)
			{
				Token name = CheckTokenType(lcontext, TokenType.Name);
				m_FuncSymbol = lcontext.Scope.TryDefineLocal(name.Text);
				m_FriendlyName = string.Format("{0} (local)", name.Text);
			}
			else
			{
				Token name = CheckTokenType(lcontext, TokenType.Name);
				string firstName = name.Text;

				m_FuncSymbol = lcontext.Scope.Find(firstName);
				m_FriendlyName = firstName;

				if (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
				{
					m_TableAccessors = new List<string>();

					while (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
					{
						Token separator = lcontext.Lexer.Current;
	
						if (separator.Type != TokenType.Colon && separator.Type != TokenType.Dot)
							throw new SyntaxErrorException("unexpected symbol near '{0}'", separator.Text);
						
						lcontext.Lexer.Next();

						Token field = CheckTokenType(lcontext, TokenType.Name);

						m_FriendlyName += separator.Text + field.Text;

						if (separator.Type == TokenType.Colon)
						{
							m_MethodName = field.Text;
							break;
						}
						else
						{
							m_TableAccessors.Add(field.Text);
						}
					}
				}
			}

			m_FuncDef = new FunctionDefinitionExpression(lcontext, m_MethodName != null);
		}

		public override void Compile(Execution.VM.ByteCode bc)
		{
			using (bc.EnterSource(m_SourceRef))
			{
				if (m_Local)
				{
					bc.Emit_Literal(DynValue.Nil);
					bc.Emit_Store(m_FuncSymbol, 0, 0);
					m_FuncDef.Compile(bc, () => SetFunction(bc, 2), m_FriendlyName);
				}
				else if (m_MethodName == null)
				{
					m_FuncDef.Compile(bc, () => SetFunction(bc, 1), m_FriendlyName);
				}
				else
				{
					m_FuncDef.Compile(bc, () => SetMethod(bc), m_FriendlyName);
				}
			}
		}

		private int SetMethod(Execution.VM.ByteCode bc)
		{
			int cnt = 0;

			cnt += bc.Emit_Load(m_FuncSymbol);

			foreach (string str in m_TableAccessors)
			{
				bc.Emit_Literal(DynValue.NewString(str));
				bc.Emit_Index();
				cnt += 2;
			}

			bc.Emit_Literal(DynValue.NewString(m_MethodName));

			bc.Emit_IndexSet(0, 0);

			return 2 + cnt;
		}

		private int SetFunction(Execution.VM.ByteCode bc, int numPop)
		{
			int num = bc.Emit_Store(m_FuncSymbol, 0, 0);
			bc.Emit_Pop(numPop);
			return num + 1;
		}

	}
}
