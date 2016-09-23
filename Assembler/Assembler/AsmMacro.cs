﻿using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;

namespace Assembler
{
    partial class AsmParser
    {
        public sealed partial class MacroContext : IFlattenable<IExecutableInstruction>
        {
            /* MEM[0xff] = sp
             * MEM[0xfe] = bp
             */

            private const string Init = @"
ANDI R1, R1, 0x00
ADDI R2, R1, 0xfe
SW   R2, R1, 0xff   ; sp = 0xfe
SW   R2, R1, 0xfe   ; bp = 0xfe
";

            private const string Call = @"
LPCH R2
LPCL R0
ANDI R1, R1, 0x00   ; +0 R0 points here
BNE  R0, R1, 0x01   ; +1
ADDI R2, R2, 0x01   ; +2
ADDI R0, R0, 11     ; +3
ADDC R2, R2, R1     ; +4
LW   R3, R1, 0xff   ; +5
SW   R0, R3, 0xfe   ; +6 MEM[sp-2] = PC_L
SW   R2, R3, 0xff   ; +7 MEM[sp-1] = PC_H
ADDI R3, R3, 0xfe   ; +8
SW   R3, R1, 0xff   ; +9 sp -= 2
JMP  {0}            ; +10
;                   ; +11
";
            private const string Ret = @"
ANDI R1, R1, 0x00
LW   R3, R1, 0xfe   ; R3 = bp
LW   R2, R3, 0x00   ; R2 = MEM[bp] = bp'
SW   R2, R1, 0xfe   ; bp = bp'
ADDI R3, R3, 0x03
SW   R3, R1, 0xff   ; POP bp, PC_L, PC_H
LW   R2, R3, 0xfe   ; R2 = PC_L
LW   R3, R3, 0xff   ; R3 = PC_H
SPC  R3, R2         ; PC = {R3,R2}
";

            private const string Push = @"
ANDI R1, R1, 0x00
LW   R1, R1, 0xff   ; R1 = sp
SW   {0}, R1, 0xff  ; MEM[sp-1] = {0}
ADDI {0}, R1, 0xff
ANDI R1, R1, 0x00
SW   {0}, R1, 0xff  ; sp--
LW   {0}, {0}, 0x00 ; {0} = MEM[sp]
";

            private const string PushBp = @"
ANDI R1, R1, 0x00
LW   R1, R1, 0xff   ; R1 = sp
SW   R0, R1, 0xfe   ; MEM[sp-2] = R0
ANDI R1, R1, 0x00
LW   R0, R1, 0xfe   ; R0 = bp
LW   R1, R1, 0xff   ; R1 = sp
SW   R0, R1, 0xff   ; MEM[sp-1] = bp
ADDI R0, R1, 0xff
ANDI R1, R1, 0x00
SW   R0, R1, 0xff   ; sp--
SW   R0, R1, 0xfe   ; bp = sp
LW   R0, R0, 0xff   ; R0 = MEM[sp-1]
";

            private const string Pop = @"
ANDI R1, R1, 0x00
LW   R1, R1, 0xff
ADDI {0}, R1, 0x01
ANDI R1, R1, 0x00
SW   {0}, R1, 0xff  ; sp++
LW   {0}, {0}, 0xff  ; {0} = MEM[sp-1]
";

            private const string Halt = @"
BEQ R1, R1, 0xff
";

            public IReadOnlyList<IExecutableInstruction> Flatten()
            {
                switch (Op.Text)
                {
                    case "INIT":
                        return Parse(Init);
                    case "CALL":
                        return Parse(string.Format(Call, obj().GetText()));
                    case "RET":
                        return Parse(Ret);
                    case "HALT":
                        return Parse(Halt);
                    case "PUSH":
                        if (Rx.Text != "BP" &&
                            RegisterNumber(Rx) == 1)
                            throw new ApplicationException("Cannot PUSH R1!");
                        return Parse(Rx.Text == "BP" ? PushBp : string.Format(Push, Rx.Text));
                    case "POP":
                        if (RegisterNumber(Rx) == 1)
                            throw new ApplicationException("Cannot POP R1!");
                        return Parse(string.Format(Pop, Rx.Text));
                    default:
                        throw new InvalidOperationException();
                }
            }

            private static IReadOnlyList<IExecutableInstruction> Parse(string str)
            {
                var lexer = new AsmLexer(new AntlrInputStream(str));
                var parser = new AsmParser(new CommonTokenStream(lexer));
                var prog = parser.prog();
                return
                    prog.line()
                        .SelectMany(
                                    l =>
                                    {
                                        if (l.instruction() != null)
                                            return new[] { l.instruction() as IExecutableInstruction };
                                        if (l.macro() != null)
                                            return l.macro().Flatten();
                                        return Enumerable.Empty<IExecutableInstruction>();
                                    }).ToList();
            }
        }
    }
}