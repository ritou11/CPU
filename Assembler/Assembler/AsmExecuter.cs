﻿using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace Assembler
{
    public class AsmExecuter
    {
        public delegate void OnBreakPointEventHandler(string line);

        public event OnBreakPointEventHandler OnBreakPoint;

        private readonly List<IExecutableInstruction> m_Instructions;

        private readonly Dictionary<int, string> m_Lines;

        private readonly Dictionary<string, int> m_Symbols;

        public Context CPU { get; private set; }

        public AsmExecuter()
        {
            m_Instructions = new List<IExecutableInstruction>();
            m_Lines = new Dictionary<int, string>();
            m_Symbols = new Dictionary<string, int>();

            CPU = new Context
                      {
                          Registers = new byte[4],
                          Ram = new byte[256]
                      };
        }

        public void Feed(TextReader reader)
        {
            var lexer = new AsmLexer(new AntlrInputStream(reader));
            var parser = new AsmParser(new CommonTokenStream(lexer));
            var prog = parser.prog();

            foreach (var context in prog.line())
                Parse(context);

            var id = 0;
            while (id < m_Instructions.Count)
            {
                if (m_Symbols.ContainsValue(id))
                    OnBreakPoint?.Invoke(m_Lines[id]);

                var symbol = m_Instructions[id].Execute(CPU);
                if (symbol == null)
                {
                    id ++;
                    continue;
                }

                if (!m_Symbols.TryGetValue(symbol, out id))
                    throw new KeyNotFoundException($"Symbol {symbol} not found.");
            }
        }

        private void Parse(AsmParser.LineContext context)
        {
            if (context.label() != null)
            {
                var lbl = context.label().Name().Symbol.Text;
                if (m_Symbols.ContainsKey(lbl))
                    throw new ApplicationException("duplicate labels");
                m_Symbols.Add(lbl, m_Instructions.Count);
            }

            if (context.instruction() != null)
            {
                m_Lines.Add(m_Instructions.Count, context.GetText());
                m_Instructions.Add(context.instruction());
            }
        }
    }
}