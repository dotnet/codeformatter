class C1
{
    int _field;

    void M()
    {
        this. /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        this._field = 42;
        // after comment
    }
}
