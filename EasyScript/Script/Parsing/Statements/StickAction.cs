﻿using ECDevice;
namespace EasyScript.Parsing.Statements;

abstract class StickAction : Statement
{
    protected readonly ECKey Key;
    protected readonly string KeyName;
    protected readonly string Direction;

    public StickAction(ECKey key, string keyname, string direcion)
    {
        Key = key;
        KeyName = keyname.ToUpper();
        Direction = direcion.ToUpper();
    }

    protected void ReleasePrevious(Assembly.Assembler assembler)
    {
        if (!assembler.StickMapping.ContainsKey(Key.KeyCode))
            return;
        assembler.StickMapping[Key.KeyCode].HoldUntil = assembler.Last();
        assembler.StickMapping.Remove(Key.KeyCode);
    }


    public static int GetDirectionIndex(int x, int y)
    {
        if (x == SwitchStick.STICK_CENTER && y == SwitchStick.STICK_CENTER)
            return -1;
        x = (int)Math.Round(x / 32d);
        y = (int)Math.Round(y / 32d);
        return x >= y ? x + y : 32 - x - y;
    }
}

class StickPress : StickAction
{
    public readonly ValBase Duration;

    public StickPress(ECKey key, string keyname, string direction, ValBase duration)
        : base(key, keyname, direction)
    {
        Duration = duration;
    }

    public override void Exec(Processor processor)
    {
        var duration = Duration.Get(processor);
        if (duration > 0)
        {
            processor.GamePad.ClickButtons(Key, duration);
            Thread.Sleep(duration);
        }
    }

    protected override string _GetString(Formatter formatter)
    {
        return $"{KeyName} {Direction},{Duration.GetCodeText(formatter)}";
    }

    public override void Assemble(Assembly.Assembler assembler)
    {
        int keycode = Key.KeyCode;
        int dindex = GetDirectionIndex(Key.StickX, Key.StickY);
        if (Duration is ValRegEx)
            throw new Assembly.AssembleException(ErrorMessage.NotSupported);
        if (Duration is ValReg reg)
        {
            assembler.Add(Assembly.Instructions.AsmStoreOp.Create(reg.Index));
            assembler.Add(Assembly.Instructions.AsmStick_Standard.Create(keycode, dindex, 0));
            ReleasePrevious(assembler);
        }
        else if (Duration is ValInstant dur)
        {
            int duration = dur.Val;
            var ins = Assembly.Instructions.AsmStick_Standard.Create(keycode, dindex, duration);
            if (ins.Success)
            {
                assembler.Add(ins);
                ReleasePrevious(assembler);
            }
            else if (ins == Assembly.Instruction.Failed.OutOfRange)
            {
                assembler.Add(Assembly.Instructions.AsmStick_Hold.Create(keycode, dindex));
                ReleasePrevious(assembler);
                assembler.StickMapping[keycode] = assembler.Last() as Assembly.Instructions.AsmStick_Hold;
                assembler.Add(Assembly.Instructions.AsmWait.Create(duration));
                assembler.Add(Assembly.Instructions.AsmEmpty.Create());
                ReleasePrevious(assembler);
            }
        }
        else
            throw new Assembly.AssembleException(ErrorMessage.NotImplemented);
    }
}

class StickDown : StickAction
{
    public StickDown(ECKey key, string keyname, string direction)
        : base(key, keyname, direction)
    { }

    public override void Exec(Processor processor)
    {
        processor.GamePad.PressButtons(Key);
    }

    protected override string _GetString(Formatter formatter)
    {
        return $"{KeyName} {Direction}";
    }

    public override void Assemble(Assembly.Assembler assembler)
    {
        assembler.Add(Assembly.Instructions.AsmStick_Hold.Create(Key.KeyCode, GetDirectionIndex(Key.StickX, Key.StickY)));
        ReleasePrevious(assembler);
        assembler.StickMapping[Key.KeyCode] = assembler.Last() as Assembly.Instructions.AsmStick_Hold;
    }
}

class StickUp : StickAction
{
    public StickUp(ECKey key, string keyname)
        : base(key, keyname, "")
    { }

    public override void Exec(Processor processor)
    {
        processor.GamePad.ReleaseButtons(Key);
    }

    protected override string _GetString(Formatter formatter)
    {
        return $"{KeyName} RESET";
    }

    public override void Assemble(Assembly.Assembler assembler)
    {
        assembler.Add(Assembly.Instructions.AsmEmpty.Create());
        ReleasePrevious(assembler);
    }
}
