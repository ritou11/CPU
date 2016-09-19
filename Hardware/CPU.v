`default_nettype none
module CPU(
   input Clock,
   input Reset,
   inout [7:0] io_0, io_1, io_2, io_3,
   inout [7:0] io_4, io_5, io_6, io_7
   );
`include "CPU_INTERNAL.v"

   // links
   wire re_WE;
   wire [1:0] re_N1, re_N2, re_ND;
   wire [7:0] re_Q1, re_Q2, re_DI;

   wire io_RE, io_WE, io_read, io_write;
   wire [7:0] io_addr, io_Din, io_Dout;

   wire ra_WR;
   wire [7:0] ra_addr, ra_data, ra_q;

   wire [PC_N-1:0] ro_addr;
   wire [15:0] ro_q;

   wire cu_jump, cu_branch, cu_alusrcb, cu_writemem;
   wire cu_writereg, cu_memtoreg, cu_regdes, cu_wrflag;
   wire [2:0] cu_aluc;
   wire [3:0] cu_op;

   reg signed [PC_N-1:0] pc_imm;
   wire [PC_N-1:0] pc_pc;

   wire al_zero, al_Cin, al_Cout;
   wire [7:0] al_A, al_B, al_S;

   wire [7:0] fl_in, fl_out;

   // data path
   wire [7:0] virtual_mem_q = io_read ? io_Dout : ra_q;
   wire signed [7:0] short_imm = ro_q[7:0];

   assign cu_op = ro_q[15:12]; // Op

   assign io_RE = cu_memtoreg;
   assign io_WE = cu_writemem;
   assign io_addr = ra_addr;
   assign io_Din = re_Q2;

   assign re_N1 = ro_q[11:10]; // Rs
   assign re_N2 = ro_q[9:8]; // Rt
   assign re_ND = cu_regdes ? ro_q[7:6] : ro_q[9:8]; // REGDES ? Rd : Rt
   assign re_DI = cu_memtoreg ? virtual_mem_q : al_S;
   assign re_WE = cu_writereg;

   assign ra_addr = al_S;
   assign ra_data = re_Q2;
   assign ra_WR = cu_writemem & ~io_write;

   assign ro_addr = pc_pc;

   always @(*)
      if (cu_jump) // JMP is long jump
         pc_imm <= ro_q[PC_N-1:0]; // long Imm
      else
         pc_imm <= short_imm; // signed extension of short Imm

   assign al_A = re_Q1;
   assign al_B = cu_alusrcb ? ro_q[7:0] : re_Q2;
   assign al_Cin = fl_out[1];

   assign fl_in = cu_wrflag ? {5'b0, al_Cout, al_zero, 1'b0} : fl_out;

   // main modules
   reg4_8 re(
      .Clock(Clock), .Reset(Reset),
      .N1(re_N1), .Q1(re_Q1),
      .N2(re_N2), .Q2(re_Q2),
      .ND(re_ND), .DI(re_DI), .REG_WE(re_WE));

   IO_PORT io(
      .addr(io_addr), .RE(io_RE), .WE(io_WE),
      .Din(io_Din), .Dout(io_Dout),
      .io_read(io_read), .io_write(io_write),
      .IO0(io_0), .IO1(io_1), .IO2(io_2), .IO3(io_3),
      .IO4(io_4), .IO5(io_5), .IO6(io_6), .IO7(io_7));

   lpm_ram_256_8 ra(
      .Clock(Clock),
      .address(ra_addr), .data(ra_data),
      .wren(ra_WR), .q(ra_q));

   lpm_rom_256_16 ro(
      .Clock(Clock),
      .address(ro_addr), .q(ro_q));

   ctrlunit cu(
      .OP(cu_op), .ZERO(al_zero),
      .JUMP(cu_jump), .BRANCH(cu_branch),
      .ALUC(cu_aluc), .WRITEMEM(cu_writemem),
      .WRITEREG(cu_writereg), .MEMTOREG(cu_memtoreg),
      .REGDES(cu_regdes), .WRFLAG(cu_wrflag),
      .ALUSRCB(cu_alusrcb));

   instrconunit pc(
      .Clock(Clock), .Reset(Reset),
      .BRANCH(cu_branch), .JUMP(cu_jump),
      .imm(pc_imm), .PC(pc_pc));

   alu alu(
      .CS(cu_aluc), .data_a(al_A), .data_b(al_B), .carry_in(al_Cin),
      .S(al_S), .zero(al_zero), .carry_out(al_Cout));

   Flag fl(
      .Clock(Clock), .Reset(Reset),
      .Flagin(fl_in), .Flagout(fl_out));

endmodule
