class C1
{
    int _field1;
    string _field2;
    internal string field3;

    void Use(int i) { } 

    void M()
    {
        Use(_field1);
        Use(_field2);
        Use(field3);
        Use(this._field1);
        Use(this._field2);
        Use(this.field3);
    }
}
