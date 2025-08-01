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

                    case AddressingMode.Displacement:
                        // Direct memory addressing: [disp32] - ModR/M = 00 reg 101
                        _runtime.WriteModRM(0b00, regOpcode, 0b101);
                        _runtime.WriteInt32(operand.Displacement);
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
                else if (dest.Mode == AddressingMode.Register && (src.Mode == AddressingMode.Memory || src.Mode == AddressingMode.IndexedDisplacement || src.Mode == AddressingMode.Displacement))
                {
                    // MOV reg, mem
                    byte opcode = dest.Size == OperandSize.Byte ? (byte)0x8A : (byte)0x8B;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(src, (byte)dest.Register);
                }
                else if ((dest.Mode == AddressingMode.Memory || dest.Mode == AddressingMode.IndexedDisplacement || dest.Mode == AddressingMode.Displacement) && src.Mode == AddressingMode.Register)
                {
                    // MOV mem, reg
                    byte opcode = src.Size == OperandSize.Byte ? (byte)0x88 : (byte)0x89;
                    _runtime.WriteByte(opcode);
                    EncodeOperand(dest, (byte)src.Register);
                }
                else if ((dest.Mode == AddressingMode.Memory || dest.Mode == AddressingMode.IndexedDisplacement || dest.Mode == AddressingMode.Displacement) && src.Mode == AddressingMode.Immediate)
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

            public ScriptBuilder MovFromVariable(Register dest, void* address)
            {
                if (address == null)
                    throw new ArgumentNullException(nameof(address), "Address cannot be null.");
                
                if (dest == Register.EAX)
                {
                    // Use special encoding for EAX: MOV EAX, [moffs32]
                    _runtime.WriteByte(0xA1);
                    _runtime.WriteInt32((int)address);
                }
                else
                {
                    // Use standard encoding: MOV reg, [disp32]
                    _runtime.WriteByte(0x8B); // MOV r32, r/m32
                    _runtime.WriteByte((byte)(0x05 + ((byte)dest << 3))); // ModR/M: 00 reg 101 (disp32)
                    _runtime.WriteInt32((int)address);
                }
                return this;
            }

            public ScriptBuilder MovToVariable(void* address, Register src)
            {
                if (address == null)
                    throw new ArgumentNullException(nameof(address), "Address cannot be null.");
                
                if (src == Register.EAX)
                {
                    // Use special encoding for EAX: MOV [moffs32], EAX
                    _runtime.WriteByte(0xA3);
                    _runtime.WriteInt32((int)address);
                }
                else
                {
                    // Use standard encoding: MOV [disp32], reg
                    _runtime.WriteByte(0x89); // MOV r/m32, r32
                    _runtime.WriteByte((byte)(0x05 + ((byte)src << 3))); // ModR/M: 00 reg 101 (disp32)
                    _runtime.WriteInt32((int)address);
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
            // Removed duplicate Popf() - using the one from later in the file

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

            // System instructions for OS development
            public ScriptBuilder Lgdt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x01);
                EncodeOperand(operand, 2);
                return this;
            }

            public ScriptBuilder Lidt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x01);
                EncodeOperand(operand, 3);
                return this;
            }

            public ScriptBuilder Sgdt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x01);
                EncodeOperand(operand, 0);
                return this;
            }

            public ScriptBuilder Sidt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x01);
                EncodeOperand(operand, 1);
                return this;
            }

            public ScriptBuilder Lldt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 2);
                return this;
            }

            public ScriptBuilder Sldt(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 0);
                return this;
            }

            public ScriptBuilder Ltr(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 3);
                return this;
            }

            public ScriptBuilder Str(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 1);
                return this;
            }

            // Control register operations - complete versions with proper overloads
            public ScriptBuilder MovCrToReg(Register reg, Register crReg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x20);
                _runtime.WriteModRM(0b11, (byte)crReg, (byte)reg);
                return this;
            }

            public ScriptBuilder MovRegToCr(Register crReg, Register reg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x22);
                _runtime.WriteModRM(0b11, (byte)crReg, (byte)reg);
                return this;
            }

            // Debug register operations - renamed to avoid conflicts
            public ScriptBuilder MovDrToReg(Register reg, Register drReg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x21);
                _runtime.WriteModRM(0b11, (byte)drReg, (byte)reg);
                return this;
            }

            public ScriptBuilder MovRegToDr(Register drReg, Register reg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x23);
                _runtime.WriteModRM(0b11, (byte)drReg, (byte)reg);
                return this;
            }

            // Test register operations - renamed to avoid conflicts (mostly obsolete but included for completeness)
            public ScriptBuilder MovTrToReg(Register reg, Register trReg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x24);
                _runtime.WriteModRM(0b11, (byte)trReg, (byte)reg);
                return this;
            }

            public ScriptBuilder MovRegToTr(Register trReg, Register reg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x26);
                _runtime.WriteModRM(0b11, (byte)trReg, (byte)reg);
                return this;
            }

            // Memory management instructions
            public ScriptBuilder Invd() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x08); return this; }
            public ScriptBuilder Wbinvd() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x09); return this; }

            public ScriptBuilder Invlpg(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x01);
                EncodeOperand(operand, 7);
                return this;
            }

            // Segment loading instructions
            public ScriptBuilder Lss(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xB2);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Lfs(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xB4);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Lgs(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xB5);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Privilege level instructions
            public ScriptBuilder Arpl(Operand dest, Register src)
            {
                _runtime.WriteByte(0x63);
                EncodeOperand(dest, (byte)src);
                return this;
            }

            public ScriptBuilder Lar(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x02);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Lsl(Register dest, Operand src)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x03);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Verr(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 4);
                return this;
            }

            public ScriptBuilder Verw(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x00);
                EncodeOperand(operand, 5);
                return this;
            }

            // Task switching instructions
            public ScriptBuilder Clts() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x06); return this; }

            // Model-specific register operations
            public ScriptBuilder Rdmsr() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x32); return this; }
            public ScriptBuilder Wrmsr() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x30); return this; }

            // Time stamp counter
            public ScriptBuilder Rdtsc() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x31); return this; }
            public ScriptBuilder Rdtscp() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xF9); return this; }

            // Performance monitoring
            public ScriptBuilder Rdpmc() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x33); return this; }

            // System management mode
            public ScriptBuilder Rsm() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0xAA); return this; }

            // CPU identification
            public ScriptBuilder Cpuid() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0xA2); return this; }

            // Memory fencing and serialization
            public ScriptBuilder Mfence() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0xAE); _runtime.WriteByte(0xF0); return this; }
            public ScriptBuilder Sfence() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0xAE); _runtime.WriteByte(0xF8); return this; }
            public ScriptBuilder Lfence() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0xAE); _runtime.WriteByte(0xE8); return this; }

            // Prefetch instructions
            public ScriptBuilder Prefetch0(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x18);
                EncodeOperand(operand, 1);
                return this;
            }

            public ScriptBuilder Prefetch1(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x18);
                EncodeOperand(operand, 2);
                return this;
            }

            public ScriptBuilder Prefetch2(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x18);
                EncodeOperand(operand, 3);
                return this;
            }

            public ScriptBuilder Prefetchnta(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0x18);
                EncodeOperand(operand, 0);
                return this;
            }

            // Cache control instructions
            public ScriptBuilder Clflush(Operand operand)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xAE);
                EncodeOperand(operand, 7);
                return this;
            }

            // Monitor/Mwait instructions
            public ScriptBuilder Monitor() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC8); return this; }
            public ScriptBuilder Mwait() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC9); return this; }

            // Virtual machine extensions
            public ScriptBuilder Vmcall() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC1); return this; }
            public ScriptBuilder Vmlaunch() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC2); return this; }
            public ScriptBuilder Vmresume() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC3); return this; }
            public ScriptBuilder Vmxoff() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xC4); return this; }

            // Secure virtual machine extensions
            public ScriptBuilder Skinit() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xDE); return this; }
            public ScriptBuilder Stgi() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xDC); return this; }
            public ScriptBuilder Clgi() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x01); _runtime.WriteByte(0xDD); return this; }

            // Advanced debugging
            public ScriptBuilder Icebp() { _runtime.WriteByte(0xF1); return this; } // Undocumented breakpoint
            public ScriptBuilder Ud2() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x0B); return this; } // Undefined instruction

            // Far control transfers
            public ScriptBuilder CallFar(short segment, int offset)
            {
                _runtime.WriteByte(0x9A);
                _runtime.WriteInt32(offset);
                _runtime.WriteInt16(segment);
                return this;
            }

            public ScriptBuilder JmpFar(short segment, int offset)
            {
                _runtime.WriteByte(0xEA);
                _runtime.WriteInt32(offset);
                _runtime.WriteInt16(segment);
                return this;
            }

            public ScriptBuilder RetFar() { _runtime.WriteByte(0xCB); return this; }
            public ScriptBuilder RetFar(short stackAdjust) 
            { 
                _runtime.WriteByte(0xCA); 
                _runtime.WriteInt16(stackAdjust);
                return this; 
            }

            // Segment override prefixes
            public ScriptBuilder SegCs() { _runtime.WriteByte(0x2E); return this; }
            public ScriptBuilder SegSs() { _runtime.WriteByte(0x36); return this; }
            public ScriptBuilder SegDs() { _runtime.WriteByte(0x3E); return this; }
            public ScriptBuilder SegEs() { _runtime.WriteByte(0x26); return this; }
            public ScriptBuilder SegFs() { _runtime.WriteByte(0x64); return this; }
            public ScriptBuilder SegGs() { _runtime.WriteByte(0x65); return this; }

            // Address size override
            public ScriptBuilder AddrSize() { _runtime.WriteByte(0x67); return this; }

            // Floating point control (x87 FPU basics)
            public ScriptBuilder Finit() { _runtime.WriteByte(0x9B); _runtime.WriteByte(0xDB); _runtime.WriteByte(0xE3); return this; }
            public ScriptBuilder Fninit() { _runtime.WriteByte(0xDB); _runtime.WriteByte(0xE3); return this; }
            public ScriptBuilder Fclex() { _runtime.WriteByte(0x9B); _runtime.WriteByte(0xDB); _runtime.WriteByte(0xE2); return this; }
            public ScriptBuilder Fnclex() { _runtime.WriteByte(0xDB); _runtime.WriteByte(0xE2); return this; }
            public ScriptBuilder Fwait() { _runtime.WriteByte(0x9B); return this; }

            // Basic FPU load/store
            public ScriptBuilder Fld(Operand operand)
            {
                if (operand.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte(0xD9);
                    EncodeOperand(operand, 0);
                }
                else // Assume double (8 bytes)
                {
                    _runtime.WriteByte(0xDD);
                    EncodeOperand(operand, 0);
                }
                return this;
            }

            public ScriptBuilder Fst(Operand operand)
            {
                if (operand.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte(0xD9);
                    EncodeOperand(operand, 2);
                }
                else
                {
                    _runtime.WriteByte(0xDD);
                    EncodeOperand(operand, 2);
                }
                return this;
            }

            public ScriptBuilder Fstp(Operand operand)
            {
                if (operand.Size == OperandSize.Dword)
                {
                    _runtime.WriteByte(0xD9);
                    EncodeOperand(operand, 3);
                }
                else
                {
                    _runtime.WriteByte(0xDD);
                    EncodeOperand(operand, 3);
                }
                return this;
            }

            // FPU arithmetic
            public ScriptBuilder Fadd() { _runtime.WriteByte(0xDE); _runtime.WriteByte(0xC1); return this; }
            public ScriptBuilder Fsub() { _runtime.WriteByte(0xDE); _runtime.WriteByte(0xE9); return this; }
            public ScriptBuilder Fmul() { _runtime.WriteByte(0xDE); _runtime.WriteByte(0xC9); return this; }
            public ScriptBuilder Fdiv() { _runtime.WriteByte(0xDE); _runtime.WriteByte(0xF9); return this; }

            // MMX/SSE basics (for completeness)
            public ScriptBuilder Emms() { _runtime.WriteByte(0x0F); _runtime.WriteByte(0x77); return this; }

            // Pause instruction (for spin loops)
            public ScriptBuilder Pause() { _runtime.WriteByte(0xF3); _runtime.WriteByte(0x90); return this; }

            // LAHF/SAHF for flag manipulation
            public ScriptBuilder Lahf() { _runtime.WriteByte(0x9F); return this; }
            public ScriptBuilder Sahf() { _runtime.WriteByte(0x9E); return this; }

            // Additional string operations
            public ScriptBuilder Ins(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0x6C : (byte)0x6D;
                _runtime.WriteByte(opcode);
                return this;
            }

            public ScriptBuilder Outs(OperandSize size = OperandSize.Dword)
            {
                WriteOperandSizePrefix(size);
                byte opcode = size == OperandSize.Byte ? (byte)0x6E : (byte)0x6F;
                _runtime.WriteByte(opcode);
                return this;
            }

            // Load flags
            public ScriptBuilder Popf() { _runtime.WriteByte(0x9D); return this; }

            // XLAT instruction
            public ScriptBuilder Xlat() { _runtime.WriteByte(0xD7); return this; }

            // Load all registers
            public ScriptBuilder Popa() { _runtime.WriteByte(0x61); return this; }
            public ScriptBuilder Pusha() { _runtime.WriteByte(0x60); return this; }

            // Byte swap
            public ScriptBuilder Bswap(Register reg)
            {
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte((byte)(0xC8 + (byte)reg));
                return this;
            }

            // Advanced bit manipulation (newer instructions)
            public ScriptBuilder Popcnt(Register dest, Operand src)
            {
                _runtime.WriteByte(0xF3);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xB8);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Lzcnt(Register dest, Operand src)
            {
                _runtime.WriteByte(0xF3);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xBD);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            public ScriptBuilder Tzcnt(Register dest, Operand src)
            {
                _runtime.WriteByte(0xF3);
                _runtime.WriteByte(0x0F);
                _runtime.WriteByte(0xBC);
                EncodeOperand(src, (byte)dest);
                return this;
            }

            // Atomic operations for multiprocessor systems
            public ScriptBuilder Xadd(Operand dest, Register src)
            {
                WriteOperandSizePrefix(dest.Size);
                _runtime.WriteByte(0x0F);
                byte opcode = dest.Size == OperandSize.Byte ? (byte)0xC0 : (byte)0xC1;
                _runtime.WriteByte(opcode);
                EncodeOperand(dest, (byte)src);
                return this;
            }

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

            // Literal assembly parser - allows direct assembly code input
            public ScriptBuilder Literal(string assembly)
            {
                if (string.IsNullOrWhiteSpace(assembly))
                    return this;

                // Parse the assembly line
                string cleanAsm = assembly.Trim().ToLowerInvariant();
                string[] parts = cleanAsm.Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length == 0)
                    return this;

                string instruction = parts[0];

                try
                {
                    switch (instruction)
                    {
                        // Data movement
                        case "mov":
                            return ParseMov(parts);
                        case "lea":
                            return ParseLea(parts);
                        case "xchg":
                            return ParseXchg(parts);

                        // Arithmetic
                        case "add":
                            return ParseArithmetic(parts, (d, s) => Add(d, s));
                        case "sub":
                            return ParseArithmetic(parts, (d, s) => Sub(d, s));
                        case "mul":
                            return ParseUnary(parts, o => Mul(o));
                        case "imul":
                            return ParseImul(parts);
                        case "div":
                            return ParseUnary(parts, o => Div(o));
                        case "idiv":
                            return ParseUnary(parts, o => Idiv(o));
                        case "inc":
                            return ParseUnary(parts, o => Inc(o));
                        case "dec":
                            return ParseUnary(parts, o => Dec(o));
                        case "neg":
                            return ParseUnary(parts, o => Neg(o));

                        // Logical
                        case "and":
                            return ParseArithmetic(parts, (d, s) => And(d, s));
                        case "or":
                            return ParseArithmetic(parts, (d, s) => Or(d, s));
                        case "xor":
                            return ParseArithmetic(parts, (d, s) => Xor(d, s));
                        case "not":
                            return ParseUnary(parts, o => Not(o));
                        case "test":
                            return ParseArithmetic(parts, (d, s) => Test(d, s));
                        case "cmp":
                            return ParseArithmetic(parts, (d, s) => Cmp(d, s));

                        // Shifts
                        case "shl":
                            return ParseShift(parts, (d, s) => Shl(d, s));
                        case "shr":
                            return ParseShift(parts, (d, s) => Shr(d, s));
                        case "sar":
                            return ParseShift(parts, (d, s) => Sar(d, s));
                        case "rol":
                            return ParseShift(parts, (d, s) => Rol(d, s));
                        case "ror":
                            return ParseShift(parts, (d, s) => Ror(d, s));

                        // Stack operations
                        case "push":
                            return ParseUnary(parts, o => Push(o));
                        case "pop":
                            return ParseUnary(parts, o => Pop(o));

                        // Control flow
                        case "jmp":
                            return ParseJump(parts);
                        case "call":
                            return ParseCall(parts);
                        case "ret":
                            return ParseRet(parts);

                        // Conditional jumps
                        case "je":
                        case "jz":
                            return ParseConditionalJump(parts, Condition.Zero);
                        case "jne":
                        case "jnz":
                            return ParseConditionalJump(parts, Condition.NotZero);
                        case "jl":
                        case "jnge":
                            return ParseConditionalJump(parts, Condition.Less);
                        case "jle":
                        case "jng":
                            return ParseConditionalJump(parts, Condition.LessEqual);
                        case "jg":
                        case "jnle":
                            return ParseConditionalJump(parts, Condition.Greater);
                        case "jge":
                        case "jnl":
                            return ParseConditionalJump(parts, Condition.GreaterEqual);
                        case "jb":
                        case "jc":
                        case "jnae":
                            return ParseConditionalJump(parts, Condition.Carry);
                        case "jbe":
                        case "jna":
                            return ParseConditionalJump(parts, Condition.BelowEqual);
                        case "ja":
                        case "jnbe":
                            return ParseConditionalJump(parts, Condition.Above);
                        case "jae":
                        case "jnb":
                        case "jnc":
                            return ParseConditionalJump(parts, Condition.NoCarry);
                        case "jo":
                            return ParseConditionalJump(parts, Condition.Overflow);
                        case "jno":
                            return ParseConditionalJump(parts, Condition.NoOverflow);
                        case "js":
                            return ParseConditionalJump(parts, Condition.Sign);
                        case "jns":
                            return ParseConditionalJump(parts, Condition.NoSign);
                        case "jp":
                        case "jpe":
                            return ParseConditionalJump(parts, Condition.Parity);
                        case "jnp":
                        case "jpo":
                            return ParseConditionalJump(parts, Condition.NoParity);

                        // Loop instructions
                        case "loop":
                            return ParseLoop(parts, (label) => Loop(label));
                        case "loope":
                        case "loopz":
                            return ParseLoop(parts, (label) => Loope(label));
                        case "loopne":
                        case "loopnz":
                            return ParseLoop(parts, (label) => Loopne(label));

                        // Miscellaneous
                        case "nop":
                            return Nop();
                        case "cdq":
                            return Cdq();
                        case "pushf":
                            return Pushf();
                        case "popf":
                            return Popf();
                        case "clc":
                            return Clc();
                        case "stc":
                            return Stc();
                        case "cmc":
                            return Cmc();
                        case "cld":
                            return Cld();
                        case "std":
                            return Std();
                        case "cli":
                            return Cli();
                        case "sti":
                            return Sti();

                        // String instructions with prefixes
                        case "rep":
                            return Rep();
                        case "repe":
                        case "repz":
                            return Repe();
                        case "repne":
                        case "repnz":
                            return Repne();

                        // String operations
                        case "movs":
                        case "movsb":
                            return ParseStringOperation(parts, size => Movs(size), OperandSize.Byte);
                        case "movsw":
                            return ParseStringOperation(parts, size => Movs(size), OperandSize.Word);
                        case "movsd":
                            return ParseStringOperation(parts, size => Movs(size), OperandSize.Dword);
                        case "cmps":
                        case "cmpsb":
                            return ParseStringOperation(parts, size => Cmps(size), OperandSize.Byte);
                        case "cmpsw":
                            return ParseStringOperation(parts, size => Cmps(size), OperandSize.Word);
                        case "cmpsd":
                            return ParseStringOperation(parts, size => Cmps(size), OperandSize.Dword);
                        case "scas":
                        case "scasb":
                            return ParseStringOperation(parts, size => Scas(size), OperandSize.Byte);
                        case "scasw":
                            return ParseStringOperation(parts, size => Scas(size), OperandSize.Word);
                        case "scasd":
                            return ParseStringOperation(parts, size => Scas(size), OperandSize.Dword);
                        case "lods":
                        case "lodsb":
                            return ParseStringOperation(parts, size => Lods(size), OperandSize.Byte);
                        case "lodsw":
                            return ParseStringOperation(parts, size => Lods(size), OperandSize.Word);
                        case "lodsd":
                            return ParseStringOperation(parts, size => Lods(size), OperandSize.Dword);
                        case "stos":
                        case "stosb":
                            return ParseStringOperation(parts, size => Stos(size), OperandSize.Byte);
                        case "stosw":
                            return ParseStringOperation(parts, size => Stos(size), OperandSize.Word);
                        case "stosd":
                            return ParseStringOperation(parts, size => Stos(size), OperandSize.Dword);

                        // Bit operations
                        case "bt":
                            return ParseBitOperation(parts, (d, s) => Bt(d, s));
                        case "bts":
                            return ParseBitOperation(parts, (d, s) => Bts(d, s));
                        case "btr":
                            return ParseBitOperation(parts, (d, s) => Btr(d, s));
                        case "btc":
                            return ParseBitOperation(parts, (d, s) => Btc(d, s));
                        case "bsf":
                            return ParseBitScan(parts, (d, s) => Bsf(d, s));
                        case "bsr":
                            return ParseBitScan(parts, (d, s) => Bsr(d, s));

                        // Conditional operations
                        case "sete":
                        case "setz":
                            return ParseSetcc(parts, Condition.Zero);
                        case "setne":
                        case "setnz":
                            return ParseSetcc(parts, Condition.NotZero);
                        case "setl":
                        case "setnge":
                            return ParseSetcc(parts, Condition.Less);
                        case "setle":
                        case "setng":
                            return ParseSetcc(parts, Condition.LessEqual);
                        case "setg":
                        case "setnle":
                            return ParseSetcc(parts, Condition.Greater);
                        case "setge":
                        case "setnl":
                            return ParseSetcc(parts, Condition.GreaterEqual);
                        case "setb":
                        case "setc":
                        case "setnae":
                            return ParseSetcc(parts, Condition.Carry);
                        case "setbe":
                        case "setna":
                            return ParseSetcc(parts, Condition.BelowEqual);
                        case "seta":
                        case "setnbe":
                            return ParseSetcc(parts, Condition.Above);
                        case "setae":
                        case "setnb":
                        case "setnc":
                            return ParseSetcc(parts, Condition.NoCarry);
                        case "seto":
                            return ParseSetcc(parts, Condition.Overflow);
                        case "setno":
                            return ParseSetcc(parts, Condition.NoOverflow);
                        case "sets":
                            return ParseSetcc(parts, Condition.Sign);
                        case "setns":
                            return ParseSetcc(parts, Condition.NoSign);
                        case "setp":
                        case "setpe":
                            return ParseSetcc(parts, Condition.Parity);
                        case "setnp":
                        case "setpo":
                            return ParseSetcc(parts, Condition.NoParity);

                        // Conditional moves
                        case "cmove":
                        case "cmovz":
                            return ParseCmov(parts, Condition.Zero);
                        case "cmovne":
                        case "cmovnz":
                            return ParseCmov(parts, Condition.NotZero);
                        case "cmovl":
                        case "cmovnge":
                            return ParseCmov(parts, Condition.Less);
                        case "cmovle":
                        case "cmovng":
                            return ParseCmov(parts, Condition.LessEqual);
                        case "cmovg":
                        case "cmovnle":
                            return ParseCmov(parts, Condition.Greater);
                        case "cmovge":
                        case "cmovnl":
                            return ParseCmov(parts, Condition.GreaterEqual);
                        case "cmovb":
                        case "cmovc":
                        case "cmovnae":
                            return ParseCmov(parts, Condition.Carry);
                        case "cmovbe":
                        case "cmovna":
                            return ParseCmov(parts, Condition.BelowEqual);
                        case "cmova":
                        case "cmovnbe":
                            return ParseCmov(parts, Condition.Above);
                        case "cmovae":
                        case "cmovnb":
                        case "cmovnc":
                            return ParseCmov(parts, Condition.NoCarry);
                        case "cmovo":
                            return ParseCmov(parts, Condition.Overflow);
                        case "cmovno":
                            return ParseCmov(parts, Condition.NoOverflow);
                        case "cmovs":
                            return ParseCmov(parts, Condition.Sign);
                        case "cmovns":
                            return ParseCmov(parts, Condition.NoSign);
                        case "cmovp":
                        case "cmovpe":
                            return ParseCmov(parts, Condition.Parity);
                        case "cmovnp":
                        case "cmovpo":
                            return ParseCmov(parts, Condition.NoParity);

                        // Double precision shifts
                        case "shld":
                            return ParseDoubleShift(parts, (d, s, c) => Shld(d, s, c));
                        case "shrd":
                            return ParseDoubleShift(parts, (d, s, c) => Shrd(d, s, c));

                        // Arithmetic with carry/borrow
                        case "adc":
                            return ParseArithmetic(parts, (d, s) => Adc(d, s));
                        case "sbb":
                            return ParseArithmetic(parts, (d, s) => Sbb(d, s));

                        // Exchange operations
                        case "cmpxchg":
                            return ParseCmpxchg(parts);
                        case "cmpxchg8b":
                            return ParseUnary(parts, o => Cmpxchg8b(o));
                        case "xadd":
                            return ParseXadd(parts);

                        // Segment operations
                        case "lds":
                            return ParseSegmentLoad(parts, (d, s) => Lds(d, s));
                        case "les":
                            return ParseSegmentLoad(parts, (d, s) => Les(d, s));
                        case "lfs":
                            return ParseSegmentLoad(parts, (d, s) => Lfs(d, s));
                        case "lgs":
                            return ParseSegmentLoad(parts, (d, s) => Lgs(d, s));
                        case "lss":
                            return ParseSegmentLoad(parts, (d, s) => Lss(d, s));

                        // Move with zero/sign extension
                        case "movzx":
                            return ParseMovExtend(parts, (d, s) => Movzx(d, s));
                        case "movsx":
                            return ParseMovExtend(parts, (d, s) => Movsx(d, s));

                        // System instructions
                        case "lgdt":
                            return ParseUnary(parts, o => Lgdt(o));
                        case "lidt":
                            return ParseUnary(parts, o => Lidt(o));
                        case "sgdt":
                            return ParseUnary(parts, o => Sgdt(o));
                        case "sidt":
                            return ParseUnary(parts, o => Sidt(o));
                        case "lldt":
                            return ParseUnary(parts, o => Lldt(o));
                        case "sldt":
                            return ParseUnary(parts, o => Sldt(o));
                        case "ltr":
                            return ParseUnary(parts, o => Ltr(o));
                        case "str":
                            return ParseUnary(parts, o => Str(o));

                        // Control register operations
                        case "mov_cr_to_reg":
                            return ParseControlRegMove(parts, true);
                        case "mov_reg_to_cr":
                            return ParseControlRegMove(parts, false);

                        // Debug register operations
                        case "mov_dr_to_reg":
                            return ParseDebugRegMove(parts, true);
                        case "mov_reg_to_dr":
                            return ParseDebugRegMove(parts, false);

                        // Test register operations
                        case "mov_tr_to_reg":
                            return ParseTestRegMove(parts, true);
                        case "mov_reg_to_tr":
                            return ParseTestRegMove(parts, false);

                        // Cache operations
                        case "invd":
                            return Invd();
                        case "wbinvd":
                            return Wbinvd();
                        case "invlpg":
                            return ParseUnary(parts, o => Invlpg(o));
                        case "clflush":
                            return ParseUnary(parts, o => Clflush(o));

                        // Memory barriers
                        case "mfence":
                            return Mfence();
                        case "sfence":
                            return Sfence();
                        case "lfence":
                            return Lfence();

                        // Prefetch instructions
                        case "prefetch0":
                            return ParseUnary(parts, o => Prefetch0(o));
                        case "prefetch1":
                            return ParseUnary(parts, o => Prefetch1(o));
                        case "prefetch2":
                            return ParseUnary(parts, o => Prefetch2(o));
                        case "prefetchnta":
                            return ParseUnary(parts, o => Prefetchnta(o));

                        // Processor identification
                        case "cpuid":
                            return Cpuid();

                        // Time stamp operations
                        case "rdtsc":
                            return Rdtsc();
                        case "rdtscp":
                            return Rdtscp();
                        case "rdpmc":
                            return Rdpmc();

                        // Model specific registers
                        case "rdmsr":
                            return Rdmsr();
                        case "wrmsr":
                            return Wrmsr();

                        // Monitor/mwait
                        case "monitor":
                            return Monitor();
                        case "mwait":
                            return Mwait();

                        // Interrupt operations
                        case "int":
                            return ParseInt(parts);
                        case "into":
                            return Into();
                        case "iret":
                            return Iret();

                        // I/O operations
                        case "in":
                            return ParseIn(parts);
                        case "out":
                            return ParseOut(parts);
                        case "ins":
                        case "insb":
                            return ParseStringOperation(parts, size => Ins(size), OperandSize.Byte);
                        case "insw":
                            return ParseStringOperation(parts, size => Ins(size), OperandSize.Word);
                        case "insd":
                            return ParseStringOperation(parts, size => Ins(size), OperandSize.Dword);
                        case "outs":
                        case "outsb":
                            return ParseStringOperation(parts, size => Outs(size), OperandSize.Byte);
                        case "outsw":
                            return ParseStringOperation(parts, size => Outs(size), OperandSize.Word);
                        case "outsd":
                            return ParseStringOperation(parts, size => Outs(size), OperandSize.Dword);

                        // ASCII operations
                        case "aaa":
                            return Aaa();
                        case "aas":
                            return Aas();
                        case "aam":
                            return Aam();
                        case "aad":
                            return Aad();
                        case "daa":
                            return Daa();
                        case "das":
                            return Das();

                        // Miscellaneous operations
                        case "hlt":
                            return Hlt();
                        case "wait":
                        case "fwait":
                            return Wait();
                        case "lock":
                            return Lock();
                        case "lahf":
                            return Lahf();
                        case "sahf":
                            return Sahf();
                        case "xlat":
                        case "xlatb":
                            return Xlat();
                        case "popa":
                            return Popa();
                        case "pusha":
                            return Pusha();
                        case "bswap":
                            return ParseBswap(parts);
                        case "pause":
                            return Pause();
                        case "emms":
                            return Emms();
                        case "rsm":
                            return Rsm();
                        case "clts":
                            return Clts();

                        // Advanced bit counting
                        case "popcnt":
                            return ParseBitCount(parts, (d, s) => Popcnt(d, s));
                        case "lzcnt":
                            return ParseBitCount(parts, (d, s) => Lzcnt(d, s));
                        case "tzcnt":
                            return ParseBitCount(parts, (d, s) => Tzcnt(d, s));

                        // Security operations
                        case "arpl":
                            return ParseArpl(parts);
                        case "lar":
                            return ParseBitCount(parts, (d, s) => Lar(d, s));
                        case "lsl":
                            return ParseBitCount(parts, (d, s) => Lsl(d, s));
                        case "verr":
                            return ParseUnary(parts, o => Verr(o));
                        case "verw":
                            return ParseUnary(parts, o => Verw(o));

                        // Stack frame operations
                        case "bound":
                            return ParseBound(parts);
                        case "enter":
                            return ParseEnter(parts);
                        case "leave":
                            return Leave();

                        // Far operations
                        case "call_far":
                            return ParseCallFar(parts);
                        case "jmp_far":
                            return ParseJmpFar(parts);
                        case "ret_far":
                            return ParseRetFar(parts);

                        // Segment prefixes
                        case "cs:":
                            return SegCs();
                        case "ss:":
                            return SegSs();
                        case "ds:":
                            return SegDs();
                        case "es:":
                            return SegEs();
                        case "fs:":
                            return SegFs();
                        case "gs:":
                            return SegGs();

                        // Address size prefix
                        case "addr16":
                        case "addr32":
                            return AddrSize();

                        // Virtualization
                        case "vmcall":
                            return Vmcall();
                        case "vmlaunch":
                            return Vmlaunch();
                        case "vmresume":
                            return Vmresume();
                        case "vmxoff":
                            return Vmxoff();
                        case "skinit":
                            return Skinit();
                        case "stgi":
                            return Stgi();
                        case "clgi":
                            return Clgi();

                        // Debug operations
                        case "icebp":
                            return Icebp();
                        case "ud2":
                            return Ud2();

                        // FPU operations
                        case "finit":
                            return Finit();
                        case "fninit":
                            return Fninit();
                        case "fclex":
                            return Fclex();
                        case "fnclex":
                            return Fnclex();
                        case "fld":
                            return ParseFpuLoad(parts, o => Fld(o));
                        case "fst":
                            return ParseFpuStore(parts, o => Fst(o));
                        case "fstp":
                            return ParseFpuStore(parts, o => Fstp(o));
                        case "fadd":
                            return Fadd();
                        case "fsub":
                            return Fsub();
                        case "fmul":
                            return Fmul();
                        case "fdiv":
                            return Fdiv();

                        default:
                            throw new NotSupportedException($"Instruction '{instruction}' is not supported in Literal()");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse assembly instruction '{assembly}': {ex.Message}", ex);
                }
            }

            private ScriptBuilder ParseMov(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("MOV requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseOperand(parts[2]);
                return Mov(dest, src);
            }

            private ScriptBuilder ParseLea(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("LEA requires destination register and source memory operand");

                var destReg = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return Lea(destReg, src);
            }

            private ScriptBuilder ParseXchg(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("XCHG requires two operands");

                var op1 = ParseOperand(parts[1]);
                var op2 = ParseOperand(parts[2]);
                return Xchg(op1, op2);
            }

            private ScriptBuilder ParseArithmetic(string[] parts, Func<Operand, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Arithmetic instruction requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseUnary(string[] parts, Func<Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("Unary instruction requires one operand");

                var operand = ParseOperand(parts[1]);
                return operation(operand);
            }

            private ScriptBuilder ParseImul(string[] parts)
            {
                if (parts.Length == 2)
                {
                    // Single operand IMUL
                    var operand = ParseOperand(parts[1]);
                    return Imul(operand);
                }
                else if (parts.Length == 3)
                {
                    // Two operand IMUL
                    var dest = ParseOperand(parts[1]);
                    var src = ParseOperand(parts[2]);
                    if (dest.Mode != AddressingMode.Register)
                        throw new ArgumentException("IMUL destination must be a register");
                    return Imul(dest, src);
                }
                else
                {
                    throw new ArgumentException("IMUL requires 1 or 2 operands");
                }
            }

            private ScriptBuilder ParseShift(string[] parts, Func<Operand, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Shift instruction requires destination and count operands");

                var dest = ParseOperand(parts[1]);
                var count = ParseOperand(parts[2]);
                return operation(dest, count);
            }

            private ScriptBuilder ParseJump(string[] parts)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("JMP requires a target");

                string target = parts[1];
                return Jmp(target);
            }

            private ScriptBuilder ParseCall(string[] parts)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("CALL requires a target");

                string target = parts[1];
                // Check if it's a label or operand
                if (IsLabel(target))
                {
                    return Call(target);
                }
                else
                {
                    var operand = ParseOperand(target);
                    return Call(operand);
                }
            }

            private ScriptBuilder ParseRet(string[] parts)
            {
                if (parts.Length == 1)
                {
                    return Ret();
                }
                else if (parts.Length == 2)
                {
                    if (short.TryParse(parts[1], out short stackAdjust))
                    {
                        return Ret(stackAdjust);
                    }
                    else
                    {
                        throw new ArgumentException("RET stack adjustment must be a valid 16-bit integer");
                    }
                }
                else
                {
                    throw new ArgumentException("RET takes 0 or 1 operand");
                }
            }

            private ScriptBuilder ParseConditionalJump(string[] parts, Condition condition)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("Conditional jump requires a target label");

                string target = parts[1];
                return Jcc(condition, target);
            }

            private ScriptBuilder ParseLoop(string[] parts, Func<string, ScriptBuilder> operation)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("Loop instruction requires a target label");

                string target = parts[1];
                return operation(target);
            }

            private Operand ParseOperand(string operandStr)
            {
                operandStr = operandStr.Trim();

                // Remove trailing comma if present
                if (operandStr.EndsWith(","))
                    operandStr = operandStr.Substring(0, operandStr.Length - 1);

                // Check for memory operand [...]
                if (operandStr.StartsWith("[") && operandStr.EndsWith("]"))
                {
                    return ParseMemoryOperand(operandStr.Substring(1, operandStr.Length - 2));
                }

                // Check for immediate value (starts with digit or negative sign)
                if (char.IsDigit(operandStr[0]) || operandStr[0] == '-' || operandStr.StartsWith("0x"))
                {
                    return ParseImmediateOperand(operandStr);
                }

                // Must be a register
                return Operand.Reg(ParseRegister(operandStr));
            }

            private Operand ParseMemoryOperand(string memoryExpr)
            {
                memoryExpr = memoryExpr.Trim();

                // Parse expressions like:
                // ebp+8, ebp-4, ebx+esi*2+16, etc.

                Register baseReg = Register.EAX;
                Register indexReg = Register.EAX;
                byte scale = 1;
                int displacement = 0;
                bool hasBase = false;
                bool hasIndex = false;

                // Split by + and - while keeping the operators
                var tokens = SplitMemoryExpression(memoryExpr);

                int sign = 1;
                foreach (var token in tokens)
                {
                    if (token == "+")
                    {
                        sign = 1;
                        continue;
                    }
                    if (token == "-")
                    {
                        sign = -1;
                        continue;
                    }

                    // Check if it's a scaled index (reg*scale)
                    if (token.Contains("*"))
                    {
                        var scaleParts = token.Split('*');
                        if (scaleParts.Length == 2)
                        {
                            indexReg = ParseRegister(scaleParts[0]);
                            if (byte.TryParse(scaleParts[1], out byte parsedScale))
                            {
                                scale = parsedScale;
                                hasIndex = true;
                            }
                        }
                    }
                    // Check if it's a register
                    else if (IsRegister(token))
                    {
                        var reg = ParseRegister(token);
                        if (!hasBase)
                        {
                            baseReg = reg;
                            hasBase = true;
                        }
                        else if (!hasIndex)
                        {
                            indexReg = reg;
                            hasIndex = true;
                        }
                    }
                    // Must be a displacement
                    else if (int.TryParse(token, out int disp) || 
                             (token.StartsWith("0x") && int.TryParse(token.Substring(2), global::System.Globalization.NumberStyles.HexNumber, null, out disp)))
                    {
                        displacement += sign * disp;
                    }

                    sign = 1; // Reset sign for next token
                }

                if (hasIndex)
                {
                    return Operand.Mem(baseReg, indexReg, scale, displacement);
                }
                else if (hasBase)
                {
                    return Operand.Mem(baseReg, displacement);
                }
                else
                {
                    // Direct memory addressing with just displacement - use special direct addressing mode
                    return new Operand 
                    { 
                        Mode = AddressingMode.Displacement, 
                        Displacement = displacement, 
                        Size = OperandSize.Dword 
                    };
                }
            }

            private List<string> SplitMemoryExpression(string expr)
            {
                var tokens = new List<string>();
                var current = new global::System.Text.StringBuilder();

                for (int i = 0; i < expr.Length; i++)
                {
                    char c = expr[i];
                    if (c == '+' || c == '-')
                    {
                        if (current.Length > 0)
                        {
                            tokens.Add(current.ToString().Trim());
                            current.Clear();
                        }
                        tokens.Add(c.ToString());
                    }
                    else
                    {
                        current.Append(c);
                    }
                }

                if (current.Length > 0)
                {
                    tokens.Add(current.ToString().Trim());
                }

                return tokens;
            }

            private Operand ParseImmediateOperand(string immediateStr)
            {
                int value;
                if (immediateStr.StartsWith("0x"))
                {
                    // Hexadecimal
                    value = int.Parse(immediateStr.Substring(2), global::System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    // Decimal
                    value = int.Parse(immediateStr);
                }

                return Operand.Imm(value);
            }

            private Register ParseRegister(string regStr)
            {
                regStr = regStr.ToLowerInvariant().Trim();
                
                return regStr switch
                {
                    "eax" => Register.EAX,
                    "ecx" => Register.ECX,
                    "edx" => Register.EDX,
                    "ebx" => Register.EBX,
                    "esp" => Register.ESP,
                    "ebp" => Register.EBP,
                    "esi" => Register.ESI,
                    "edi" => Register.EDI,
                    _ => throw new ArgumentException($"Unknown register: {regStr}")
                };
            }

            private bool IsRegister(string token)
            {
                token = token.ToLowerInvariant().Trim();
                return token == "eax" || token == "ecx" || token == "edx" || token == "ebx" ||
                       token == "esp" || token == "ebp" || token == "esi" || token == "edi";
            }

            private bool IsLabel(string token)
            {
                // Labels are anything that's not a register and doesn't look like a number
                return !IsRegister(token) && 
                       !char.IsDigit(token[0]) && 
                       !token.StartsWith("-") && 
                       !token.StartsWith("0x") &&
                       !token.StartsWith("[");
            }

            // Additional parsing methods for all the new instructions
            private ScriptBuilder ParseStringOperation(string[] parts, Func<OperandSize, ScriptBuilder> operation, OperandSize defaultSize)
            {
                return operation(defaultSize);
            }

            private ScriptBuilder ParseBitOperation(string[] parts, Func<Operand, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Bit operation requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseBitScan(string[] parts, Func<Register, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Bit scan operation requires destination register and source operand");

                var dest = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseSetcc(string[] parts, Condition condition)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("SETcc requires a destination operand");

                var dest = ParseOperand(parts[1]);
                return Setcc(condition, dest);
            }

            private ScriptBuilder ParseCmov(string[] parts, Condition condition)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("CMOVcc requires destination register and source operand");

                var dest = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return Cmovcc(condition, dest, src);
            }

            private ScriptBuilder ParseDoubleShift(string[] parts, Func<Operand, Register, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 4)
                    throw new ArgumentException("Double shift requires destination, source register, and count operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseRegister(parts[2]);
                var count = ParseOperand(parts[3]);
                return operation(dest, src, count);
            }

            private ScriptBuilder ParseCmpxchg(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("CMPXCHG requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseRegister(parts[2]);
                return Cmpxchg(dest, src);
            }

            private ScriptBuilder ParseXadd(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("XADD requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseRegister(parts[2]);
                return Xadd(dest, src);
            }

            private ScriptBuilder ParseSegmentLoad(string[] parts, Func<Register, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Segment load requires destination register and source operand");

                var dest = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseMovExtend(string[] parts, Func<Register, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Move with extension requires destination register and source operand");

                var dest = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseControlRegMove(string[] parts, bool crToReg)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Control register move requires two register operands");

                var reg1 = ParseRegister(parts[1]);
                var reg2 = ParseRegister(parts[2]);
                
                if (crToReg)
                    return MovCrToReg(reg1, reg2);
                else
                    return MovRegToCr(reg1, reg2);
            }

            private ScriptBuilder ParseDebugRegMove(string[] parts, bool drToReg)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Debug register move requires two register operands");

                var reg1 = ParseRegister(parts[1]);
                var reg2 = ParseRegister(parts[2]);
                
                if (drToReg)
                    return MovDrToReg(reg1, reg2);
                else
                    return MovRegToDr(reg1, reg2);
            }

            private ScriptBuilder ParseTestRegMove(string[] parts, bool trToReg)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Test register move requires two register operands");

                var reg1 = ParseRegister(parts[1]);
                var reg2 = ParseRegister(parts[2]);
                
                if (trToReg)
                    return MovTrToReg(reg1, reg2);
                else
                    return MovRegToTr(reg1, reg2);
            }

            private ScriptBuilder ParseInt(string[] parts)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("INT requires an interrupt number");

                if (byte.TryParse(parts[1], out byte interrupt))
                {
                    return Int(interrupt);
                }
                else if (parts[1].StartsWith("0x") && byte.TryParse(parts[1].Substring(2), global::System.Globalization.NumberStyles.HexNumber, null, out interrupt))
                {
                    return Int(interrupt);
                }
                else
                {
                    throw new ArgumentException("INT interrupt number must be a valid byte value");
                }
            }

            private ScriptBuilder ParseIn(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("IN requires destination register and port");

                var dest = ParseRegister(parts[1]);
                var portStr = parts[2];

                if (byte.TryParse(portStr, out byte port))
                {
                    return In(dest, port);
                }
                else if (portStr.StartsWith("0x") && byte.TryParse(portStr.Substring(2), global::System.Globalization.NumberStyles.HexNumber, null, out port))
                {
                    return In(dest, port);
                }
                else if (IsRegister(portStr))
                {
                    var portReg = ParseRegister(portStr);
                    return In(dest, portReg);
                }
                else
                {
                    throw new ArgumentException("IN port must be a byte value or DX register");
                }
            }

            private ScriptBuilder ParseOut(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("OUT requires port and source register");

                var portStr = parts[1];
                var src = ParseRegister(parts[2]);

                if (byte.TryParse(portStr, out byte port))
                {
                    return Out(port, src);
                }
                else if (portStr.StartsWith("0x") && byte.TryParse(portStr.Substring(2), global::System.Globalization.NumberStyles.HexNumber, null, out port))
                {
                    return Out(port, src);
                }
                else if (IsRegister(portStr))
                {
                    var portReg = ParseRegister(portStr);
                    return Out(portReg, src);
                }
                else
                {
                    throw new ArgumentException("OUT port must be a byte value or DX register");
                }
            }

            private ScriptBuilder ParseBswap(string[] parts)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("BSWAP requires a register operand");

                var reg = ParseRegister(parts[1]);
                return Bswap(reg);
            }

            private ScriptBuilder ParseBitCount(string[] parts, Func<Register, Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Bit count operation requires destination register and source operand");

                var dest = ParseRegister(parts[1]);
                var src = ParseOperand(parts[2]);
                return operation(dest, src);
            }

            private ScriptBuilder ParseArpl(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("ARPL requires destination and source operands");

                var dest = ParseOperand(parts[1]);
                var src = ParseRegister(parts[2]);
                return Arpl(dest, src);
            }

            private ScriptBuilder ParseBound(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("BOUND requires index register and bounds operand");

                var index = ParseRegister(parts[1]);
                var bounds = ParseOperand(parts[2]);
                return Bound(index, bounds);
            }

            private ScriptBuilder ParseEnter(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("ENTER requires allocation size and nesting level");

                if (short.TryParse(parts[1], out short allocSize) && byte.TryParse(parts[2], out byte nestingLevel))
                {
                    return Enter(allocSize, nestingLevel);
                }
                else
                {
                    throw new ArgumentException("ENTER parameters must be valid numeric values");
                }
            }

            private ScriptBuilder ParseCallFar(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Far CALL requires segment and offset");

                if (short.TryParse(parts[1], out short segment) && int.TryParse(parts[2], out int offset))
                {
                    return CallFar(segment, offset);
                }
                else
                {
                    throw new ArgumentException("Far CALL parameters must be valid numeric values");
                }
            }

            private ScriptBuilder ParseJmpFar(string[] parts)
            {
                if (parts.Length < 3)
                    throw new ArgumentException("Far JMP requires segment and offset");

                if (short.TryParse(parts[1], out short segment) && int.TryParse(parts[2], out int offset))
                {
                    return JmpFar(segment, offset);
                }
                else
                {
                    throw new ArgumentException("Far JMP parameters must be valid numeric values");
                }
            }

            private ScriptBuilder ParseRetFar(string[] parts)
            {
                if (parts.Length == 1)
                {
                    return RetFar();
                }
                else if (parts.Length == 2)
                {
                    if (short.TryParse(parts[1], out short stackAdjust))
                    {
                        return RetFar(stackAdjust);
                    }
                    else
                    {
                        throw new ArgumentException("Far RET stack adjustment must be a valid 16-bit integer");
                    }
                }
                else
                {
                    throw new ArgumentException("Far RET takes 0 or 1 operand");
                }
            }

            private ScriptBuilder ParseFpuLoad(string[] parts, Func<Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("FPU load requires a source operand");

                var operand = ParseOperand(parts[1]);
                return operation(operand);
            }

            private ScriptBuilder ParseFpuStore(string[] parts, Func<Operand, ScriptBuilder> operation)
            {
                if (parts.Length < 2)
                    throw new ArgumentException("FPU store requires a destination operand");

                var operand = ParseOperand(parts[1]);
                return operation(operand);
            }
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
                Logger.DriverLog(this, "Runtime Executor is already installed and active.",GenericLogger.Status.Error);
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

            Logger.DriverLog(this, "Runtime Executor installed successfully.",GenericLogger.Status.Ok);
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

            Logger.DriverLog(this, "Runtime Executor restarted successfully.",GenericLogger.Status.Ok);
            return SGenericStatus.Success("Runtime Executor restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is already active.",GenericLogger.Status.Error);
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is already active.");
            }
            IsActive = true;

            Logger.DriverLog(this, "Runtime Executor started successfully.",GenericLogger.Status.Ok);
            return SGenericStatus.Success("Runtime Executor started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be stopped.",GenericLogger.Status.Error);
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active.");
            }
            IsActive = false;
            Logger.DriverLog(this, "Runtime Executor stopped successfully.",GenericLogger.Status.Ok);
            return SGenericStatus.Success("Runtime Executor stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be uninstalled.",GenericLogger.Status.Error);
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active and cannot be uninstalled.");
            }
            IsActive = false;
            Logger.DriverLog(this, "Uninstalling Runtime Executor...",GenericLogger.Status.Info);
            Logger = null;
            DriverList.UnregisterDriver(this);
            return SGenericStatus.Success("Runtime Executor uninstalled successfully.");
        }
    }
}
