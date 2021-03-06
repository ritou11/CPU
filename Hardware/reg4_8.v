`default_nettype none
module reg4_8(
   input Clock,
   input Reset,
   // read channel 1
   input [1:0] N1, // reg id
   output reg [7:0] Q1, // data
   // read channel 2
   input [1:0] N2, // reg id
   output reg [7:0] Q2, // data
   // write channel
   input [1:0] ND, // reg id
   input [7:0] DI, // data
   input REG_WE // enable
`ifdef SIMULATION
   ,
   output [7:0] R0,
   output [7:0] R1,
   output [7:0] R2,
   output [7:0] R3
`endif
);

   reg [7:0] registers[0:3];

`ifdef SIMULATION
   assign R0 = registers[0];
   assign R1 = registers[1];
   assign R2 = registers[2];
   assign R3 = registers[3];
`endif

   // read channel 1
   always @(*)
      Q1 <= registers[N1];

   // read channel 2
   always @(*)
      Q2 <= registers[N2];

   // write channel
   always @(posedge Clock, negedge Reset)
      if (~Reset)
         begin
            registers[2'h0] = 8'b0;
            registers[2'h1] = 8'b0;
            registers[2'h2] = 8'b0;
            registers[2'h3] = 8'b0;
         end
      else if (REG_WE)
         registers[ND] = DI;

endmodule
