namespace PhotoRetouch;

public static class CurveLookupTables
{
    public static byte[] CreateIdentity()
    {
        byte[] lookup = new byte[256];
        for (int index = 0; index < lookup.Length; index++)
        {
            lookup[index] = (byte)index;
        }

        return lookup;
    }
}
