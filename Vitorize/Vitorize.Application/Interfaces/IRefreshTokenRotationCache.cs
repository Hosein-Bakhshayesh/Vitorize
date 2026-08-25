using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// Remembers, for a few seconds, what one refresh-token rotation produced.
    ///
    /// Rotation is single-use: the presented token is revoked the instant the replacement is issued.
    /// That is correct, but it makes an ordinary race fatal. Two browser tabs waking at the same
    /// moment, or a page reload arriving just after a rotation whose replacement never reached the
    /// browser's cookie jar, both present the same now-spent token — and a strict single-use rule
    /// answers 401, which destroys a session the user never meant to end. That is the "I have to
    /// clear my cookies" symptom.
    ///
    /// Only hashes are stored in the database, so the replacement plaintext cannot be recovered from
    /// <c>ReplacedByTokenHash</c> after the fact, and persisting plaintext refresh tokens is not an
    /// option. Instead the result is held briefly in memory, keyed by the hash of the token that was
    /// spent, so every caller racing inside the window receives the same canonical pair rather than a
    /// second, divergent rotation.
    ///
    /// Deliberately in-process and short-lived: the value never touches disk, never reaches a log, and
    /// is gone within the window. A multi-instance deployment would need a shared store; Vitorize runs
    /// a single API instance.
    /// </summary>
    public interface IRefreshTokenRotationCache
    {
        /// <summary>How long a rotation result stays replayable.</summary>
        static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(60);

        /// <summary>The pair a previous rotation of this token produced, while still in the window.</summary>
        bool TryGet(string spentTokenHash, out AuthResponseDto? result);

        /// <summary>Records what this rotation produced so racing callers converge on it.</summary>
        void Remember(string spentTokenHash, AuthResponseDto result);

        /// <summary>
        /// Drops a remembered rotation, ending its window early.
        ///
        /// Present so the expiry path can be exercised deliberately instead of by sleeping through the
        /// window, which would make the test both slow and timing-dependent. Nothing in the request
        /// path calls it; entries otherwise disappear on their own.
        /// </summary>
        void Forget(string spentTokenHash);
    }
}
