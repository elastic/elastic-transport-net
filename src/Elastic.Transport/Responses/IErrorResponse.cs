namespace Elastic.Transport;

/// <summary>
/// Base class for types representing client specific errors. This may be provided by clients to be used for deserialisation of the HTTP body for non-success status codes.
/// </summary>
public abstract class ErrorResponseBase
{
	/// <summary>
	/// May be called by transport to establish whether the instance represents a valid, complete error.
	/// <para>This may not always be the case if the error is partially deserialised on the response.</para>
	/// </summary>
	public abstract bool HasError();
}

/// <summary>
/// 
/// </summary>
internal sealed class EmptyError : ErrorResponseBase
{
	public static readonly EmptyError Instance = new();

	/// <inheritdoc cref="ErrorResponseBase.HasError" />
	public override bool HasError() => false;
}
