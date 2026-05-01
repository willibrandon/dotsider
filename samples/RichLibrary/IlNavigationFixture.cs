namespace RichLibrary;

/// <summary>
/// Fixture class exercising diverse IL opcode patterns for go-to-definition testing.
/// </summary>
public class IlNavigationFixture
{
    private int _counter;

    /// <summary>Calls a local method (produces call MethodDef token).</summary>
    public string CallLocalMethod() { _counter++; return LocalTarget(); }

    /// <summary>The target of local method calls.</summary>
    public string LocalTarget() { _counter++; return "target"; }

    /// <summary>Overloaded method (1 arg).</summary>
    public int Overloaded(int x) { _counter++; return x; }

    /// <summary>Overloaded method (2 args).</summary>
    public int Overloaded(int x, int y) { _counter++; return x + y; }

    /// <summary>Calls the 2-arg overload.</summary>
    public int CallOverloadedTwoArg() { _counter++; return Overloaded(1, 2); }

    /// <summary>Calls Console.WriteLine (MemberRef to external assembly).</summary>
    public void CallExternal() { _counter++; Console.WriteLine("hello"); }

    /// <summary>Uses castclass with a local TypeDef token.</summary>
    public IlNavigationFixture CastToSelf(object o) { _counter++; return (IlNavigationFixture)o; }

    /// <summary>Reads an instance field (ldfld FieldDef token).</summary>
    public int ReadInstanceField() { _counter++; return _counter; }

    /// <summary>Creates self via newobj.</summary>
    public IlNavigationFixture CreateSelf() { _counter++; return new IlNavigationFixture(); }

    /// <summary>Boxes an int.</summary>
    public object BoxInt(int x) { _counter++; return x; }

    /// <summary>Generic usage.</summary>
    public List<int> GenericUsage() { _counter++; return [1, 2, 3]; }

    /// <summary>Accesses System.String.Empty (produces ldsfld MemberRef to external field).</summary>
    public string GetStringEmpty() { _counter++; return string.Empty; }

    /// <summary>Constructs a type locally owned in the partial-facade System.Collections.dll.</summary>
    public LinkedList<int> CreateLinkedList() { _counter++; return new LinkedList<int>(); }

    /// <summary>Casts to an external type (produces castclass with TypeRef to System.IO.Stream).</summary>
    public Stream CastToExternalStream(object o) { _counter++; return (Stream)o; }
}
