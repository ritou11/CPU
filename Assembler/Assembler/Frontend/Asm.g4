grammar Asm;

/*
 * Parser Rules
 */

prog
    : (line? EOL) *
    ;

line
    : Comment
    | label Comment?
    | instruction Comment?
    ;

label
    : Name ':'
    ;

instruction
    : typeR | typeI | typeJ
    ;

typeR
    : Op=(AND | OR | ADD | SUB | SLT | SUBC | ADDC) Rd=Register ','? Rs=Register ','? Rt=Register
    ;

typeI
    : Op=(ANDI | ORI | ADDI | LW | SW) Rt=Register ','? Rs=Register ','? number
    | Op=(BEQ | BNE) Rs=Register ','? Rt=Register ','? obj
    ;

typeJ
    : Op=JMP obj
    ;

obj
    : number
    | Name
    ;

number
	: Decimal | Binary | Hexadecimal
    ;

/*
 * Lexer Rules
 */

fragment A:('a'|'A');
fragment B:('b'|'B');
fragment C:('c'|'C');
fragment D:('d'|'D');
fragment E:('e'|'E');
fragment F:('f'|'F');
fragment G:('g'|'G');
fragment H:('h'|'H');
fragment I:('i'|'I');
fragment J:('j'|'J');
fragment K:('k'|'K');
fragment L:('l'|'L');
fragment M:('m'|'M');
fragment N:('n'|'N');
fragment O:('o'|'O');
fragment P:('p'|'P');
fragment Q:('q'|'Q');
fragment R:('r'|'R');
fragment S:('s'|'S');
fragment T:('t'|'T');
fragment U:('u'|'U');
fragment V:('v'|'V');
fragment W:('w'|'W');
fragment X:('x'|'X');
fragment Y:('y'|'Y');
fragment Z:('z'|'Z');

AND : A N D;
OR  : O R;
ADD : A D D;
SUB : S U B;
SLT : S L T;
SUBC: S U B C;
ADDC: A D D C;
ANDI: A N D I;
ORI : O R I;
ADDI: A D D I;
LW  : L W;
SW  : S W;
BEQ : B E Q;
BNE : B N E;
JMP : J M P;

Decimal
    : ('-' | '+')? [0-9]+
    ;

Binary
    : '0b' [01]+
    ;

Hexadecimal
    : '0x' [0-9a-fA-F]+
    ;

Number
    : Decimal | Binary | Hexadecimal
    ;

Register
    : ('R' | 'r') [0-3]
    ;

Name
    : [_a-zA-Z] [a-zA-Z0-9\-_]*
    ;

Comment
    : ';' ~ [\r\n]*
    ;

EOL
    : '\r'? '\n'
    ;

WS
    : [ \t] -> skip
    ;
