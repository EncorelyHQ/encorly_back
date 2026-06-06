namespace EncorelyApplication.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string resource) : base($"{resource} not found.") { }
}

public class DuplicateEmailException : AppException
{
    public DuplicateEmailException() : base("A user with that email already exists.") { }
}

public class InvalidCredentialsException : AppException
{
    public InvalidCredentialsException() : base("Invalid email or password.") { }
}
