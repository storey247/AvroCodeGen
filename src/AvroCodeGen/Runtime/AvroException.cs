namespace AvroCodeGen.Runtime;

public class AvroException(string message, Exception? inner = null) : Exception(message, inner);
