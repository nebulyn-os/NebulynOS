using Cosmos.Core.Memory;
using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using Nebulyn.System.Derivatives.Drivers;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nebulyn.System.Core.Drivers
{
    public unsafe class RuntimeExecution : DriverBase
    {
        private byte* RuntimeMemory = null;
        private int RuntimeMemorySize = 0;
        private GenericLogger Logger;
        private int currentPosition = 0;

        protected override bool IsActive
        {
            get => RuntimeMemory != null;
            set
            {
                if (value && RuntimeMemory == null)
                {
                    RuntimeMemorySize = 1024;
                    RuntimeMemory = Heap.Alloc((uint)RuntimeMemorySize);
                    Clear();
                }
                else if (!value && RuntimeMemory != null)
                {
                    Heap.Free(RuntimeMemory);
                    RuntimeMemory = null;
                    RuntimeMemorySize = 0;
                }
            }
        }

        // Registers enum
        public enum Register : byte
        {
            EAX = 0,
            ECX = 1,
            EDX = 2,
            EBX = 3,
            ESP = 4,
            EBP = 5,
            ESI = 6,
            EDI = 7
        }

        // Clears memory with NOPs and resets position
        public void Clear()
        {
            if (RuntimeMemory == null) return;
            Unsafe.InitBlock(RuntimeMemory, 0xC3, (uint)RuntimeMemorySize);
            currentPosition = 0;
        }

        // Creates a new script builder
        public ScriptBuilder CreateScript()
        {
            if (!IsActive)
            {
                Start();
            }
            Clear();
            return new ScriptBuilder(this);
        }

        // Execute the compiled code
        public SGenericStatus Execute()
        {
            if (!IsActive)
            {
                Logger?.DriverLog(this, "Runtime Executor is not active. Start it first.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active.");
            }
            if (RuntimeMemory == null)
            {
                Logger?.DriverLog(this, "Runtime memory not allocated. Ensure the driver is active.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime memory not allocated.");
            }
            unsafe
            {
                Logger?.DriverLog(this, $"Executing instructions at {(ulong)RuntimeMemory:X}");
                var instructionPtr = (delegate*<void>)RuntimeMemory;
                instructionPtr();
            }
            Logger?.DriverLog(this, "Instructions executed successfully.");
            return SGenericStatus.Success("Instructions executed successfully.");
        }

        private int ExecuteInternalRaw()
        {
            return ((delegate* unmanaged<int>)RuntimeMemory)();
        }

        public SGenericStatus ExecuteWithReturn(out int returnValue)
        {
            
            if (!IsActive)
            {
                Logger?.DriverLog(this, "Runtime Executor is not active. Start it first.");
                returnValue = 0;
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active.");
            }
            if (RuntimeMemory == null)
            {
                Logger?.DriverLog(this, "Runtime memory not allocated. Ensure the driver is active.");
                returnValue = 0;
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime memory not allocated.");
            }

            Logger?.DriverLog(this, $"Executing instructions at {(ulong)RuntimeMemory:X}");

            returnValue = ExecuteInternalRaw();

            Logger?.DriverLog(this, "Instructions executed successfully.");
            return SGenericStatus.Success("Instructions executed successfully.");
        }

        // Helper methods for writing bytes to memory
        private void WriteByte(byte value)
        {
            if (currentPosition >= RuntimeMemorySize)
                throw new Exception("Runtime memory buffer overflow.");
            RuntimeMemory[currentPosition++] = value;
        }

        private void WriteInt16(short value)
        {
            if (currentPosition + 2 > RuntimeMemorySize)
                throw new Exception("Runtime memory buffer overflow.");
            *(short*)(RuntimeMemory + currentPosition) = value;
            currentPosition += 2;
        }

        private void WriteInt32(int value)
        {
            if (currentPosition + 4 > RuntimeMemorySize)
                throw new Exception("Runtime memory buffer overflow.");
            *(int*)(RuntimeMemory + currentPosition) = value;
            currentPosition += 4;
        }

        private void WriteModRM(byte mod, byte reg, byte rm)
        {
            byte modrm = (byte)((mod << 6) | ((reg & 7) << 3) | (rm & 7));
            WriteByte(modrm);
        }

        private void WriteSIB(byte scale, byte index, byte baseReg)
        {
            byte sib = (byte)((scale << 6) | ((index & 7) << 3) | (baseReg & 7));
            WriteByte(sib);
        }

        // Advanced addressing mode structures
        public enum AddressingMode
        {
            Register,
            Memory,
            Immediate,
            Displacement,
            IndexedDisplacement
        }

        public struct Operand
        {
            public AddressingMode Mode;
            public Register Register;
            public Register BaseRegister;
            public Register IndexRegister;
            public byte Scale; // 1, 2, 4, or 8
            public int Displacement;
            public int ImmediateValue;
            public OperandSize Size;

            public static Operand Reg(Register reg, OperandSize size = OperandSize.Dword)
                => new Operand { Mode = AddressingMode.Register, Register = reg, Size = size };

            public static Operand Imm(int value, OperandSize size = OperandSize.Dword)
                => new Operand { Mode = AddressingMode.Immediate, ImmediateValue = value, Size = size };

            public static Operand Mem(Register baseReg, int displacement = 0, OperandSize size = OperandSize.Dword)
                => new Operand { Mode = AddressingMode.Memory, BaseRegister = baseReg, Displacement = displacement, Size = size };

            public static Operand Mem(Register baseReg, Register indexReg, byte scale = 1, int displacement = 0, OperandSize size = OperandSize.Dword)
                => new Operand { Mode = AddressingMode.IndexedDisplacement, BaseRegister = baseReg, IndexRegister = indexReg, Scale = scale, Displacement = displacement, Size = size };
        }

        public enum OperandSize : byte
        {
            Byte = 1,
            Word = 2,
            Dword = 4
        }

        public enum Condition : byte
        {
            Overflow = 0x0,      // JO
            NoOverflow = 0x1,    // JNO
            Carry = 0x2,         // JC/JB/JNAE
            NoCarry = 0x3,       // JNC/JNB/JAE
            Zero = 0x4,          // JZ/JE
            NotZero = 0x5,       // JNZ/JNE
            BelowEqual = 0x6,    // JBE/JNA
            Above = 0x7,         // JA/JNBE
            Sign = 0x8,          // JS
            NoSign = 0x9,        // JNS
            Parity = 0xA,        // JP/JPE
            NoParity = 0xB,      // JNP/JPO
            Less = 0xC,          // JL/JNGE
            GreaterEqual = 0xD,  // JGE/JNL
            LessEqual = 0xE,     // JLE/JNG
            Greater = 0xF        // JG/JNLE
        }

        // Advanced x86 Assembler class for fluent API
        public class ScriptBuilder
        {
            private readonly RuntimeExecution _runtime;
            private readonly Dictionary<string, int> _labels = new Dictionary<string, int>();
            private readonly List<LabelReference> _labelReferences = new List<LabelReference>();

            internal ScriptBuilder(RuntimeExecution runtime)
            {
                _runtime = runtime;
            }

            private struct LabelReference
            {
                public string Label;
                public int Position;
                public bool IsShort;
            }

            // Label management
            public ScriptBuilder Label(string name)
            {
                _labels[name] = _runtime.currentPosition;
                return this;
            }

            private void AddLabelReference(string label, bool isShort = false)
            {
                _labelReferences.Add(new LabelReference
                {
                    Label = label,
                    Position = _runtime.currentPosition,
                    IsShort = isShort
                });
            }

            private void ResolveLabelReferences()
            {
                foreach (var reference in _labelReferences)
                {
                    if (!_labels.TryGetValue(reference.Label, out int targetPosition))
                        throw new Exception($"Undefined label: {reference.Label}");

                    int offset = targetPosition - (reference.Position + (reference.IsShort ? 1 : 4));
                    
                    if (reference.IsShort)
                    {
                        if (offset < -128 || offset > 127)
                            throw new Exception($"Short jump offset out of range for label {reference.Label}");
                        _runtime.RuntimeMemory[reference.Position] = (byte)offset;
                    }
                    else
                    {
                        *(int*)(_runtime.RuntimeMemory + reference.Position) = offset;
                    }
                }
                _labelReferences.Clear();
            }

            // Advanced addressing mode encoder
            private void EncodeOperand(Operand operand, byte regOpcode, bool needsRex = false)
            {
                switch (operand.Mode)
                {
                    case AddressingMode.Register:
                        _runtime.WriteModRM(0b11, regOpcode, (byte)operand.Register);
                        break;

                    case AddressingMode.Memory:
                        if (operand.BaseRegister == Register.ESP)
                        {
                            // ESP requires SIB byte
                            _runtime.WriteModRM(GetModBits(operand.Displacement), regOpcode, 0b100);
                            _runtime.WriteSIB(0, 0b100, (byte)operand.BaseRegister); // No index
                        }
                        else if (operand.BaseRegister == Register.EBP && operand.Displacement == 0)
                        {
                            // EBP with no displacement needs mod=01 and disp8=0
                            _runtime.WriteModRM(0b01, regOpcode, (byte)operand.BaseRegister);
                            _runtime.WriteByte(0x00);
                        }
                        else
                        {
                            byte mod = GetModBits(operand.Displacement);
                            _runtime.WriteModRM(mod, regOpcode, (byte)operand.BaseRegister);
                        }
                        WriteDisplacement(operand.Displacement);
                        break;

                    case AddressingMode.IndexedDisplacement:
                        byte modBits = GetModBits(operand.Displacement);
                        _runtime.WriteModRM(modBits, regOpcode, 0b100); // Use SIB
                        
                        byte scaleBits = operand.Scale switch
                        {
                            1 => 0b00,
                            2 => 0b01,
                            4 => 0b10,
                            8 => 0b11,
                            _ => throw new ArgumentException("Invalid scale factor")
                        };
                        
                        _runtime.WriteSIB(scaleBits, (byte)operand.IndexRegister, (byte)operand.BaseRegister);
                        WriteDisplacement(operand.Displacement);
                        break;
                }
            }

            private byte GetModBits(int displacement)
            {
                if (displacement == 0) return 0b00;
                if (displacement >= -128 && displacement <= 127) return 0b01;
                return 0b10;
            }

            private void WriteDisplacement(int displacement)
            {
                if (displacement == 0) return;
                
                if (displacement >= -128 && displacement <= 127)
                {
                    _runtime.WriteByte((byte)displacement);
                }
                else
                {
                    _runtime.WriteInt32(displacement);
                }
            }

            // Prefix handling for operand size overrides
            private void WriteOperandSizePrefix(OperandSize size)
            {
                if (size == OperandSize.Word)
                    _runtime.WriteByte(0x66); // Operand size override prefix
            }

            // MOV instructions with full addressing support
            public ScriptBuilder Mov(Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);

                if (dest.Mode == AddressingMode.Register && src.Mode == AddressingMode.Register)
                {
                    // MOV reg, reg
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0x88 : (byte)0x89;
                    _runtime.WriteByte(opcode);
                    _runtime.WriteModRM(0b11, (byte)src.Register, (byte)dest.Register);
                }
                else if (dest.Mode == AddressingMode.Register && src.Mode == AddressingMode.Immediate)
                {
                    // MOV reg, imm
                    if (dest.Size == OperandSize.Byte)
                    {
                        _runtime.WriteByte((byte)(0xB0 + (byte)dest.Register));
                        _runtime.WriteByte((byte)src.ImmediateValue);
                    }
                    else
                    {
                        _runtime.WriteByte((byte)(0xB8 + (byte)dest.Register));
                        if (dest.Size == OperandSize.Word)
                            _runtime.WriteInt16((short)src.ImmediateValue);
                        else
                            _runtime.WriteInt32(src.ImmediateValue);
                    }
                }
                else if (dest.Mode == AddressingMode.Register && (src.Mode == AddressingMode.Memory || src.Mode == AddressingMode.IndexedDisplacement))
                {
                    // MOV reg, mem
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0x8A : (byte)0x8B;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(src, (byte)dest.Register);
                }
                else if ((dest.Mode == AddressingMode.Memory || dest.Mode == AddressingMode.IndexedDisplacement) && src.Mode == AddressingMode.Register)
                {
                    // MOV mem, reg
                    byte opcode = src.Size == OperandSize.Byte ? (byte)0x88 : (byte)0x89;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if ((dest.Mode == AddressingMode.Memory || dest.Mode == AddressingMode.IndexedDisplacement) && src.Mode == AddressingMode.Immediate)
                {
                    // MOV mem, imm
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0xC6 : (byte)0xC7;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, 0);
                    
                    if (dest.Size == OperandSize.Byte)
                        _runtime.WriteByte((byte)src.ImmediateValue);
                    else if (dest.Size == OperandSize.Word)
                        _runtime.WriteInt16((short)src.ImmediateValue);
                    else
                        _runtime.WriteInt32(src.ImmediateValue);
                }
                else
                {
                    throw new InvalidOperationException("Invalid MOV operand combination");
                }

                return this;
            }

            // Loop instructions
            public ScriptBuilder Loop(string label)
            {
                _runtime.WriteByte(0xE2); // LOOP rel8
                AddLabelReference(label, true);
                _runtime.WriteByte(0); // Placeholder
                return this;
            }

            public ScriptBuilder Loope(string label)
            {
                _runtime.WriteByte(0xE1); // LOOPE/LOOPZ rel8
                AddLabelReference(label, true);
                _runtime.WriteByte(0); // Placeholder
                return this;
            }

            public ScriptBuilder Loopz(string label) => Loope(label); // Alias for LOOPE

            public ScriptBuilder Loopne(string label)
            {
                _runtime.WriteByte(0xE0); // LOOPNE/LOOPNZ rel8
                AddLabelReference(label, true);
                _runtime.WriteByte(0); // Placeholder
                return this;
            }

            public ScriptBuilder Loopnz(string label) => Loopne(label); // Alias for LOOPNE

            // Arithmetic instructions with full addressing support
            public ScriptBuilder Add(Operand dest, Operand src) => ArithmeticOperation(0x00, 0x80, 0, dest, src);
            public ScriptBuilder Sub(Operand dest, Operand src) => ArithmeticOperation(0x28, 0x80, 5, dest, src);
            public ScriptBuilder And(Operand dest, Operand src) => ArithmeticOperation(0x20, 0x80, 4, dest, src);
            public ScriptBuilder Or(Operand dest, Operand src) => ArithmeticOperation(0x08, 0x80, 1, dest, src);
            public ScriptBuilder Xor(Operand dest, Operand src) => ArithmeticOperation(0x30, 0x80, 6, dest, src);
            public ScriptBuilder Cmp(Operand dest, Operand src) => ArithmeticOperation(0x38, 0x80, 7, dest, src);

            private ScriptBuilder ArithmeticOperation(byte regRegOpcode, byte regImmOpcode, byte immExtension, Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);

                if (src.Mode == AddressingMode.Register)
                {
                    // op r/m, reg
                    byte opcode = dest.Size == OperandSize.Byte ? regRegOpcode : (byte)(regRegOpcode + 1);
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if (src.Mode == AddressingMode.Immediate)
                {
                    // op r/m, imm
                    if (dest.Size == OperandSize.Byte)
                    {
                        _runtime.WriteByte(regImmOpcode);
                        EncodeOperand(dest, immExtension);
                        _runtime.WriteByte((byte)src.ImmediateValue);
                    }
                    else
                    {
                        // Check if we can use 8-bit immediate with sign extension
                        if (src.ImmediateValue >= -128 && src.ImmediateValue <= 127)
                        {
                            _runtime.WriteByte((byte)(regImmOpcode + 3)); // Use 8-bit immediate with sign extension
                            EncodeOperand(dest, immExtension);
                            _runtime.WriteByte((byte)src.ImmediateValue);
                        }
                        else
                        {
                            _runtime.WriteByte((byte)(regImmOpcode + 1));
                            EncodeOperand(dest, immExtension);
                            if (dest.Size == OperandSize.Word)
                                _runtime.WriteInt16((short)src.ImmediateValue);
                            else
                                _runtime.WriteInt32(src.ImmediateValue);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid arithmetic operand combination");
                }

                return this;
            }

            // Advanced multiplication and division
            public ScriptBuilder Mul(Operand operand) => UnaryOperation(0xF6, 4, operand);
            public ScriptBuilder Imul(Operand operand) => UnaryOperation(0xF6, 5, operand);
            public ScriptBuilder Div(Operand operand) => UnaryOperation(0xF6, 6, operand);
            public ScriptBuilder Idiv(Operand operand) => UnaryOperation(0xF6, 7, operand);

            // Two-operand IMUL
            public ScriptBuilder Imul(Operand dest, Operand src)
            {
                if (dest.Mode != AddressingMode.Register)
                    throw new InvalidOperationException("IMUL destination must be a register");

                WriteOperandSizePrefix(dest.Size);
                
                if (src.Mode == AddressingMode.Immediate)
                {
                    // IMUL reg, imm
                    if (src.ImmediateValue >= -128 && src.ImmediateValue <= 127)
                    {
                        _runtime.WriteByte(0x6B);
                        _runtime.WriteModRM(0b11, (byte)dest.Register, (byte)dest.Register);
                        _runtime.WriteByte((byte)src.ImmediateValue);
                    }
                    else
                    {
                        _runtime.WriteByte(0x69);
                        _runtime.WriteModRM(0b11, (byte)dest.Register, (byte)dest.Register);
                        if (dest.Size == OperandSize.Word)
                            _runtime.WriteInt16((short)src.ImmediateValue);
                        else
                            _runtime.WriteInt32(src.ImmediateValue);
                    }
                }
                else
                {
                    // IMUL reg, r/m
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte(0xAF);
                    EncodeOperand(src, (byte)dest.Register);
                }

                return this;
            }

            private ScriptBuilder UnaryOperation(byte baseOpcode, byte extension, Operand operand)
            {
                WriteOperandSizePrefix(operand.Size);
                byte opcode = operand.Size == OperandSize.Byte ? baseOpcode : (byte)(baseOpcode + 1);
                _runtime.WriteByte(opcode);
                EncodeOperand(operand, extension);
                return this;
            }

            // Shift and rotate operations
            public ScriptBuilder Shl(Operand dest, Operand count) => ShiftOperation(4, dest, count);
            public ScriptBuilder Shr(Operand dest, Operand count) => ShiftOperation(5, dest, count);
            public ScriptBuilder Sar(Operand dest, Operand count) => ShiftOperation(7, dest, count);
            public ScriptBuilder Rol(Operand dest, Operand count) => ShiftOperation(0, dest, count);
            public ScriptBuilder Ror(Operand dest, Operand count) => ShiftOperation(1, dest, count);

            private ScriptBuilder ShiftOperation(byte extension, Operand dest, Operand count)
            {
                WriteOperandSizePrefix(dest.Size);

                if (count.Mode == AddressingMode.Immediate && count.ImmediateValue == 1)
                {
                    // shift by 1
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0xD0 : (byte)0xD1;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, extension);
                }
                else if (count.Mode == AddressingMode.Register && count.Register == Register.ECX)
                {
                    // shift by CL
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0xD2 : (byte)0xD3;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, extension);
                }
                else if (count.Mode == AddressingMode.Immediate)
                {
                    // shift by immediate
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0xC0 : (byte)0xC1;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, extension);
                    _runtime.WriteByte((byte)count.ImmediateValue);
                }
                else
                {
                    throw new InvalidOperationException("Invalid shift count operand");
                }

                return this;
            }

            // Stack operations
            public ScriptBuilder Push(Operand operand)
            {
                if (operand.Mode == AddressingMode.Register)
                {
                    _runtime.WriteByte((byte)(0x50 + (byte)operand.Register));
                }
                else if (operand.Mode == AddressingMode.Immediate)
                {
                    if (operand.ImmediateValue >= -128 && operand.ImmediateValue <= 127)
                    {
                        _runtime.WriteByte(0x6A); // PUSH imm8
                        _runtime.WriteByte((byte)operand.ImmediateValue);
                    }
                    else
                    {
                        _runtime.WriteByte(0x68); // PUSH imm32
                        _runtime.WriteInt32(operand.ImmediateValue);
                    }
                }
                else
                {
                    WriteOperandSizePrefix(operand.Size);
                    _runtime.WriteByte(0xFF);
                    EncodeOperand(operand, 6);
                }
                return this;
            }

            public ScriptBuilder Pop(Operand operand)
            {
                if (operand.Mode == AddressingMode.Register)
                {
                    _runtime.WriteByte((byte)(0x58 + (byte)operand.Register));
                }
                else
                {
                    WriteOperandSizePrefix(operand.Size);
                    _runtime.WriteByte(0x8F);
                    EncodeOperand(operand, 0);
                }
                return this;
            }

            // Increment and decrement
            public ScriptBuilder Inc(Operand operand)
            {
                if (operand.Mode == AddressingMode.Register && operand.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte((byte)(0x40 + (byte)operand.Register));
                }
                else
                {
                    WriteOperandSizePrefix(operand.Size);
                    byte opcode = operand.Size == OperandSize.Byte ? (byte)0xFE : (byte)0xFF;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(operand, 0);
                }
                return this;
            }

            public ScriptBuilder Dec(Operand operand)
            {
                if (operand.Mode == AddressingMode.Register && operand.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte((byte)(0x48 + (byte)operand.Register));
                }
                else
                {
                    WriteOperandSizePrefix(operand.Size);
                    byte opcode = operand.Size == OperandSize.Byte ? (byte)0xFE : (byte)0xFF;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(operand, 1);
                }
                return this;
            }

            // Control flow instructions
            public ScriptBuilder Jmp(string label)
            {
                _runtime.WriteByte(0xE9); // JMP rel32
                AddLabelReference(label, false);
                _runtime.WriteInt32(0); // Placeholder
                return this;
            }

            public ScriptBuilder JmpShort(string label)
            {
                _runtime.WriteByte(0xEB); // JMP rel8
                AddLabelReference(label, true);
                _runtime.WriteByte(0); // Placeholder
                return this;
            }

            // Conditional jumps
            public ScriptBuilder Jcc(Condition condition, string label, bool shortJump = false)
            {
                if (shortJump)
                {
                    _runtime.WriteByte((byte)(0x70 + (byte)condition));
                    AddLabelReference(label, true);
                    _runtime.WriteByte(0); // Placeholder
                }
                else
                {
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte((byte)(0x80 + (byte)condition));
                    AddLabelReference(label, false);
                    _runtime.WriteInt32(0); // Placeholder
                }
                return this;
            }

            // Common conditional jump aliases
            public ScriptBuilder Je(string label, bool shortJump = false) => Jcc(Condition.Zero, label, shortJump);
            public ScriptBuilder Jne(string label, bool shortJump = false) => Jcc(Condition.NotZero, label, shortJump);
            public ScriptBuilder Jl(string label, bool shortJump = false) => Jcc(Condition.Less, label, shortJump);
            public ScriptBuilder Jle(string label, bool shortJump = false) => Jcc(Condition.LessEqual, label, shortJump);
            public ScriptBuilder Jg(string label, bool shortJump = false) => Jcc(Condition.Greater, label, shortJump);
            public ScriptBuilder Jge(string label, bool shortJump = false) => Jcc(Condition.GreaterEqual, label, shortJump);
            public ScriptBuilder Jb(string label, bool shortJump = false) => Jcc(Condition.Carry, label, shortJump);
            public ScriptBuilder Jbe(string label, bool shortJump = false) => Jcc(Condition.BelowEqual, label, shortJump);
            public ScriptBuilder Ja(string label, bool shortJump = false) => Jcc(Condition.Above, label, shortJump);
            public ScriptBuilder Jae(string label, bool shortJump = false) => Jcc(Condition.NoCarry, label, shortJump);

            public ScriptBuilder Call(string label)
            {
                _runtime.WriteByte(0xE8); // CALL rel32
                AddLabelReference(label, false);
                _runtime.WriteInt32(0); // Placeholder
                return this;
            }

            public ScriptBuilder Call(Operand operand)
            {
                _runtime.WriteByte(0xFF);
                EncodeOperand(operand, 2);
                return this;
            }

            // Special instructions
            public ScriptBuilder Nop() { _runtime.WriteByte(0x90); return this; }
            public ScriptBuilder Ret() { _runtime.WriteByte(0xC3); return this; }
            public ScriptBuilder Ret(short stackAdjust) 
            { 
                _runtime.WriteByte(0xC2); 
                _runtime.WriteInt16(stackAdjust);
                return this; 
            }

            public ScriptBuilder Cdq() { _runtime.WriteByte(0x99); return this; } // Sign extend EAX to EDX:EAX
            public ScriptBuilder Pushf() { _runtime.WriteByte(0x9C); return this; }
            public ScriptBuilder Popf() { _runtime.WriteByte(0x9D); return this; }

            // LEA (Load Effective Address)
            public ScriptBuilder Lea(Register dest, Operand src)
            {
                if (src.Mode != AddressingMode.Memory && src.Mode != AddressingMode.IndexedDisplacement)
                    throw new InvalidOperationException("LEA source must be a memory operand");

                _runtime.WriteByte(0x8D);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Test instruction
            public ScriptBuilder Test(Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);

                if (src.Mode == AddressingMode.Register)
                {
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0x84 : (byte)0x85;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if (src.Mode == AddressingMode.Immediate)
                {
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0xF6 : (byte)0xF7;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, 0);
                    
                    if (dest.Size == OperandSize.Byte)
                        _runtime.WriteByte((byte)src.ImmediateValue);
                    else if (dest.Size == OperandSize.Word)
                        _runtime.WriteInt16((short)src.ImmediateValue);
                    else
                        _runtime.WriteInt32(src.ImmediateValue);
                }

                return this;
            }

            // String instructions
            public ScriptBuilder Movs(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0xA4 : (byte)0xA5;
                _runtime.WriteByte(opcode);
                return this;
            }

            public ScriptBuilder Cmps(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0xA6 : (byte)0xA7;
                _runtime.WriteByte(opcode);
                return this;
            }

            public ScriptBuilder Scas(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0xAE : (byte)0xAF;
                _runtime.WriteByte(opcode);
                return this;
            }

            public ScriptBuilder Lods(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0xAC : (byte)0xAD;
                _runtime.WriteByte(opcode);
                return this;
            }

            public ScriptBuilder Stos(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0xAA : (byte)0xAB;
                _runtime.WriteByte(opcode);
                return this;
            }

            // String instruction prefixes
            public ScriptBuilder Rep() { _runtime.WriteByte(0xF3); return this; }
            public ScriptBuilder Repe() { _runtime.WriteByte(0xF3); return this; }
            public ScriptBuilder Repz() { _runtime.WriteByte(0xF3); return this; }
            public ScriptBuilder Repne() { _runtime.WriteByte(0xF2); return this; }
            public ScriptBuilder Repnz() { _runtime.WriteByte(0xF2); return this; }

            // Bit manipulation instructions
            public ScriptBuilder Bt(Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);
                if (src.Mode == AddressingMode.Register)
                {
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte(0xA3);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if (src.Mode == AddressingMode.Immediate)
                {
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte(0xBA);
                    EncodeOperand(dest, 4);
                    _runtime.WriteByte((byte)src.ImmediateValue);
                }
                return this;
            }

            public ScriptBuilder Bts(Operand dest, Operand src) => BitOperation(0xAB, 5, dest, src);
            public ScriptBuilder Btr(Operand dest, Operand src) => BitOperation(0xB3, 6, dest, src);
            public ScriptBuilder Btc(Operand dest, Operand src) => BitOperation(0xBB, 7, dest, src);

            private ScriptBuilder BitOperation(byte regOpcode, byte immExtension, Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);
                if (src.Mode == AddressingMode.Register)
                {
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte(regOpcode);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if (src.Mode == AddressingMode.Immediate)
                {
                    _runtime.WriteByte(0x0F);
                    _runtime.WriteByte(0xBA);
                    EncodeOperand(dest, immExtension);
                    _runtime.WriteByte((byte)src.ImmediateValue);
                }
                return this;
            }

            // Bit scan instructions
            public ScriptBuilder Bsf(Register dest, Operand src)
            {
                WriteOperandSizePrefix(OperandSize.Dword);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xBC);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Bsr(Register dest, Operand src)
            {
                WriteOperandSizePrefix(OperandSize.Dword);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xBD);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Set byte on condition
            public ScriptBuilder Setcc(Condition condition, Operand dest)
            {
                if (dest.Size != OperandSize.Byte)
                    throw new InvalidOperationException("SETcc destination must be byte-sized");
                
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte((byte)(0x90 + (byte)condition));
                EncodeOperand(dest, 0);
                return this;
            }

            // Common SETcc aliases
            public ScriptBuilder Sete(Operand dest) => Setcc(Condition.Zero, dest);
            public ScriptBuilder Setne(Operand dest) => Setcc(Condition.NotZero, dest);
            public ScriptBuilder Setl(Operand dest) => Setcc(Condition.Less, dest);
            public ScriptBuilder Setle(Operand dest) => Setcc(Condition.LessEqual, dest);
            public ScriptBuilder Setg(Operand dest) => Setcc(Condition.Greater, dest);
            public ScriptBuilder Setge(Operand dest) => Setcc(Condition.GreaterEqual, dest);
            public ScriptBuilder Setb(Operand dest) => Setcc(Condition.Carry, dest);
            public ScriptBuilder Setbe(Operand dest) => Setcc(Condition.BelowEqual, dest);
            public ScriptBuilder Seta(Operand dest) => Setcc(Condition.Above, dest);
            public ScriptBuilder Setae(Operand dest) => Setcc(Condition.NoCarry, dest);

            // Conditional move instructions
            public ScriptBuilder Cmovcc(Condition condition, Register dest, Operand src)
            {
                WriteOperandSizePrefix(OperandSize.Dword);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte((byte)(0x40 + (byte)condition));
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Common CMOVcc aliases
            public ScriptBuilder Cmove(Register dest, Operand src) => Cmovcc(Condition.Zero, dest, src);
            public ScriptBuilder Cmovne(Register dest, Operand src) => Cmovcc(Condition.NotZero, dest, src);
            public ScriptBuilder Cmovl(Register dest, Operand src) => Cmovcc(Condition.Less, dest, src);
            public ScriptBuilder Cmovle(Register dest, Operand src) => Cmovcc(Condition.LessEqual, dest, src);
            public ScriptBuilder Cmovg(Register dest, Operand src) => Cmovcc(Condition.Greater, dest, src);
            public ScriptBuilder Cmovge(Register dest, Operand src) => Cmovcc(Condition.GreaterEqual, dest, src);
            public ScriptBuilder Cmovb(Register dest, Operand src) => Cmovcc(Condition.Carry, dest, src);
            public ScriptBuilder Cmovbe(Register dest, Operand src) => Cmovcc(Condition.BelowEqual, dest, src);
            public ScriptBuilder Cmova(Register dest, Operand src) => Cmovcc(Condition.Above, dest, src);
            public ScriptBuilder Cmovae(Register dest, Operand src) => Cmovcc(Condition.NoCarry, dest, src);

            // Exchange instructions
            public ScriptBuilder Xchg(Operand dest, Operand src)
            {
                WriteOperandSizePrefix(dest.Size);
                
                // Special encoding for EAX
                if (dest.Mode == AddressingMode.Register && dest.Register == Register.EAX && 
                    src.Mode == AddressingMode.Register && dest.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte((byte)(0x90 + (byte)src.Register));
                }
                else if (src.Mode == AddressingMode.Register && src.Register == Register.EAX && 
                         dest.Mode == AddressingMode.Register && dest.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte((byte)(0x90 + (byte)dest.Register));
                }
                else
                {
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0x86 : (byte)0x87;
                    _runtime.WriteByte(opcode);
                    if (src.Mode == AddressingMode.Register)
                        EncodeOperand(dest, (byte)src.Register);
                    else
                        EncodeOperand(src, (byte)dest.Register);
                }
                return this;
            }

            public ScriptBuilder Cmpxchg(Operand dest, Register src)
            {
                WriteOperandSizePrefix(dest.Size);
                _runtime.WriteByte(0x0F);
                byte opcode = dest.Size == OperandSize.Byte ? (byte)0xB0 : (byte)0xB1;
                _runtime.WriteByte(opcode);
                EncodeOperand(dest, (byte)src);
                return this;
            }

            public ScriptBuilder Cmpxchg8b(Operand dest)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xC7);
                EncodeOperand(dest, 1);
                return this;
            }

            // Double precision shift instructions
            public ScriptBuilder Shld(Operand dest, Register src, Operand count)
            {
                WriteOperandSizePrefix(dest.Size);
                _runtime.WriteByte(0x0F);
                
                if (count.Mode == AddressingMode.Register && count.Register == Register.ECX)
                {
                    _runtime.WriteByte(0xA5);
                    EncodeOperand(dest, (byte)src);
                }
                else if (count.Mode == AddressingMode.Immediate)
                {
                    _runtime.WriteByte(0xA4);
                    EncodeOperand(dest, (byte)src);
                    _runtime.WriteByte((byte)count.ImmediateValue);
                }
                return this;
            }

            public ScriptBuilder Shrd(Operand dest, Register src, Operand count)
            {
                WriteOperandSizePrefix(dest.Size);
                _runtime.WriteByte(0x0F);
                
                if (count.Mode == AddressingMode.Register && count.Register == Register.ECX)
                {
                    _runtime.WriteByte(0xAD);
                    EncodeOperand(dest, (byte)src);
                }
                else if (count.Mode == AddressingMode.Immediate)
                {
                    _runtime.WriteByte(0xAC);
                    EncodeOperand(dest, (byte)src);
                    _runtime.WriteByte((byte)count.ImmediateValue);
                }
                return this;
            }

            // Arithmetic with carry/borrow
            public ScriptBuilder Adc(Operand dest, Operand src) => ArithmeticOperation(0x10, 0x80, 2, dest, src);
            public ScriptBuilder Sbb(Operand dest, Operand src) => ArithmeticOperation(0x18, 0x80, 3, dest, src);

            // Negate and complement
            public ScriptBuilder Neg(Operand operand) => UnaryOperation(0xF6, 3, operand);
            public ScriptBuilder Not(Operand operand) => UnaryOperation(0xF6, 2, operand);

            // Flag manipulation
            public ScriptBuilder Clc() { _runtime.WriteByte(0xF8); return this; } // Clear carry
            public ScriptBuilder Stc() { _runtime.WriteByte(0xF9); return this; } // Set carry
            public ScriptBuilder Cmc() { _runtime.WriteByte(0xF5); return this; } // Complement carry
            public ScriptBuilder Cld() { _runtime.WriteByte(0xFC); return this; } // Clear direction
            public ScriptBuilder Std() { _runtime.WriteByte(0xFD); return this; } // Set direction
            public ScriptBuilder Cli() { _runtime.WriteByte(0xFA); return this; } // Clear interrupt
            public ScriptBuilder Sti() { _runtime.WriteByte(0xFB); return this; } // Set interrupt

            // ASCII adjust instructions
            public ScriptBuilder Aaa() { _runtime.WriteByte(0x37); return this; } // ASCII adjust after add
            public ScriptBuilder Aas() { _runtime.WriteByte(0x3F); return this; } // ASCII adjust after subtract
            public ScriptBuilder Aam() { _runtime.WriteByte(0xD4); _runtime.WriteByte(0x0A); return this; } // ASCII adjust after multiply
            public ScriptBuilder Aad() { _runtime.WriteByte(0xD5); _runtime.WriteByte(0x0A); return this; } // ASCII adjust before divide

            // Decimal adjust instructions
            public ScriptBuilder Daa() { _runtime.WriteByte(0x27); return this; } // Decimal adjust after add
            public ScriptBuilder Das() { _runtime.WriteByte(0x2F); return this; } // Decimal adjust after subtract

            // Processor control
            public ScriptBuilder Hlt() { _runtime.WriteByte(0xF4); return this; } // Halt
            public ScriptBuilder Wait() { _runtime.WriteByte(0x9B); return this; } // Wait for coprocessor
            public ScriptBuilder Lock() { _runtime.WriteByte(0xF0); return this; } // Lock prefix

            // Interrupt instructions
            public ScriptBuilder Int(byte interrupt)
            {
                if (interrupt == 3)
                {
                    _runtime.WriteByte(0xCC); // INT 3 (breakpoint)
                }
                else
                {
                    _runtime.WriteByte(0xCD);
                    _runtime.WriteByte(interrupt);
                }
                return this;
            }

            public ScriptBuilder Into() { _runtime.WriteByte(0xCE); return this; } // Interrupt on overflow
            public ScriptBuilder Iret() { _runtime.WriteByte(0xCF); return this; } // Interrupt return

            // I/O instructions
            public ScriptBuilder In(Register dest, byte port)
            {
                if (dest != Register.EAX)
                    throw new InvalidOperationException("IN destination must be EAX");
                
                _runtime.WriteByte(0xE4); // IN AL, imm8
                _runtime.WriteByte(port);
                return this;
            }

            public ScriptBuilder In(Register dest, Register portReg)
            {
                if (dest != Register.EAX || portReg != Register.EDX)
                    throw new InvalidOperationException("IN must use EAX and EDX");
                
                _runtime.WriteByte(0xEC); // IN AL, DX
                return this;
            }

            public ScriptBuilder Out(byte port, Register src)
            {
                if (src != Register.EAX)
                    throw new InvalidOperationException("OUT source must be EAX");
                
                _runtime.WriteByte(0xE6); // OUT imm8, AL
                _runtime.WriteByte(port);
                return this;
            }

            public ScriptBuilder Out(Register portReg, Register src)
            {
                if (src != Register.EAX || portReg != Register.EDX)
                    throw new InvalidOperationException("OUT must use EAX and EDX");
                
                _runtime.WriteByte(0xEE); // OUT DX, AL
                return this;
            }

            // Load segment registers (simplified - only common ones)
            public ScriptBuilder Lds(Register dest, Operand src)
            {
                _runtime.WriteByte(0xC5);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Les(Register dest, Operand src)
            {
                _runtime.WriteByte(0xC4);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Move with zero/sign extend
            public ScriptBuilder Movzx(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                if (src.Size == OperandSize.Byte)
                {
                    _runtime.WriteByte(0xB6); // MOVZX r32, r/m8
                }
                else if (src.Size == OperandSize.Word)
                {
                    _runtime.WriteByte(0xB7); // MOVZX r32, r/m16
                }
                else
                {
                    throw new InvalidOperationException("MOVZX source must be byte or word");
                }
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Movsx(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                if (src.Size == OperandSize.Byte)
                {
                    _runtime.WriteByte(0xBE); // MOVSX r32, r/m8
                }
                else if (src.Size == OperandSize.Word)
                {
                    _runtime.WriteByte(0xBF); // MOVSX r32, r/m16
                }
                else
                {
                    throw new InvalidOperationException("MOVSX source must be byte or word");
                }
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Advanced arithmetic
            public ScriptBuilder Adc(Register dest, Register src) => Adc(Operand.Reg(dest), Operand.Reg(src));
            public ScriptBuilder Adc(Register dest, int immediate) => Adc(Operand.Reg(dest), Operand.Imm(immediate));
            public ScriptBuilder Sbb(Register dest, Register src) => Sbb(Operand.Reg(dest), Operand.Reg(src));
            public ScriptBuilder Sbb(Register dest, int immediate) => Sbb(Operand.Reg(dest), Operand.Imm(immediate));

            // Bound instruction
            public ScriptBuilder Bound(Register index, Operand bounds)
            {
                _runtime.WriteByte(0x62);
                EncodeOperand(bounds, (byte)index);
                return this;
            }

            // Enter and Leave for stack frames
            public ScriptBuilder Enter(short allocSize, byte nestingLevel)
            {
                _runtime.WriteByte(0xC8);
                _runtime.WriteInt16(allocSize);
                _runtime.WriteByte(nestingLevel);
                return this;
            }

            public ScriptBuilder Leave() { _runtime.WriteByte(0xC9); return this; }

            // Finalize and execute
            public SGenericStatus Execute()
            {
                ResolveLabelReferences();
                return _runtime.Execute();
            }

            public SGenericStatus ExecuteWithReturn(out int returnValue)
            {
                ResolveLabelReferences();
                return _runtime.ExecuteWithReturn(out returnValue);
            }

            // Convenience methods for backward compatibility
            public ScriptBuilder Mov(Register dest, Register src) => Mov(Operand.Reg(dest), Operand.Reg(src));
            public ScriptBuilder Mov(Register dest, int immediate) => Mov(Operand.Reg(dest), Operand.Imm(immediate));
            public ScriptBuilder Add(Register dest, Register src) => Add(Operand.Reg(dest), Operand.Reg(src));
            public ScriptBuilder Add(Register dest, int immediate) => Add(Operand.Reg(dest), Operand.Imm(immediate));
            public ScriptBuilder Sub(Register dest, Register src) => Sub(Operand.Reg(dest), Operand.Reg(src));
            public ScriptBuilder Sub(Register dest, int immediate) => Sub(Operand.Reg(dest), Operand.Imm(immediate));
            public ScriptBuilder Push(Register reg) => Push(Operand.Reg(reg));
            public ScriptBuilder Pop(Register reg) => Pop(Operand.Reg(reg));
            public ScriptBuilder Inc(Register reg) => Inc(Operand.Reg(reg));
            public ScriptBuilder Dec(Register reg) => Dec(Operand.Reg(reg));
        }

        // Driver implementation methods
        public override SDriverInfo Identify()
        {
            return new SDriverInfo(
                    name: "Runtime Executor",
                    version: "1.0.0",
                    description: "A code execution driver for Nebulyn System.",
                    manufacturer: "Nebulyn Systems",
                    deviceId: "ba19bd0d-eab3-406d-9ef3-72ce5d7ec13d",
                    driverInstallType: EDriverInstallType.BuiltIn,
                    driverPurpose: EDriverPurpose.KernelExtension,
                    installationDate: DateTime.UtcNow,
                    isActive: IsActive,
                    filePath: ""
                );
        }

        public override SGenericStatus Install()
        {
            if (IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is already installed and active.");
                return SGenericStatus.Failure(EGenericResult.AlreadyExists, "Runtime Executor is already installed.");
            }

            var status = DriverList.GetDriverById("2d6aa0a6-b8f1-4321-b0b5-0a71520edae9", out IDriver aDriver);

            if (status.IsSuccess)
            {
                Logger = aDriver as GenericLogger;
                if (Logger == null)
                {
                    return SGenericStatus.Failure(EGenericResult.InvalidState, "Failed to acquire GenericLogger instance.");
                }
            }
            else
            {
                return SGenericStatus.Failure(EGenericResult.NotFound, "GenericLogger driver not found.");
            }

            DriverList.RegisterDriver(this);

            Logger.DriverLog(this, "Runtime Executor installed successfully.");
            return SGenericStatus.Success("Runtime Executor installed successfully.");
        }

        public override SGenericStatus Restart()
        {
            if (!IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active and cannot be restarted.");
            }

            IsActive = false;
            IsActive = true;

            Logger.DriverLog(this, "Runtime Executor restarted successfully.");
            return SGenericStatus.Success("Runtime Executor restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is already active.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is already active.");
            }
            IsActive = true;

            Logger.DriverLog(this, "Runtime Executor started successfully.");
            return SGenericStatus.Success("Runtime Executor started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be stopped.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active.");
            }
            IsActive = false;
            Logger.DriverLog(this, "Runtime Executor stopped successfully.");
            return SGenericStatus.Success("Runtime Executor stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be uninstalled.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active and cannot be uninstalled.");
            }
            IsActive = false;
            Logger.DriverLog(this, "Uninstalling Runtime Executor...");
            Logger = null;
            DriverList.UnregisterDriver(this);
            return SGenericStatus.Success("Runtime Executor uninstalled successfully.");
        }
    }
}
