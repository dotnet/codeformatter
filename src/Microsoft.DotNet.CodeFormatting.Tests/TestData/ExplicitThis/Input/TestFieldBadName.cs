class C1
{
    int _field;

    void M()
    {
        // Not a valid field access, can't reliably remove this.
        this.field1 = 0;
    }
}
