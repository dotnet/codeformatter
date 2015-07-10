class C1
{
    int _field;

    void M()
    {
         /* comment1 */ _field /* comment 2 */ = 0;
        // before comment
        _field = 42;
        // after comment
    }
}
