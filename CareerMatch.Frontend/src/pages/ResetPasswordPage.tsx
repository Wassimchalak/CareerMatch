import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import "./ResetPasswordPage.css";

function ResetPasswordPage() {
    // Reads query-string values from the URL.
    // Example URL:
    // http://localhost:5173/reset-password?token=abc123
    const [searchParams] = useSearchParams();

    // Used to redirect the user after the password is reset.
    const navigate = useNavigate();

    // Gets the reset token from the URL.
    const token = searchParams.get("token") ?? "";

    // Stores the new password typed by the user.
    const [newPassword, setNewPassword] = useState("");

    // Stores the password confirmation typed by the user.
    const [confirmPassword, setConfirmPassword] = useState("");

    // Controls whether the new-password field is visible.
    const [showNewPassword, setShowNewPassword] = useState(false);

    // Controls whether the confirm-password field is visible.
    const [showConfirmPassword, setShowConfirmPassword] = useState(false);

    // Displays validation or backend error messages.
    const [errorMessage, setErrorMessage] = useState("");

    // Displays a success message after resetting the password.
    const [successMessage, setSuccessMessage] = useState("");

    // Prevents multiple submissions while the request is running.
    const [isSubmitting, setIsSubmitting] = useState(false);

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        setErrorMessage("");
        setSuccessMessage("");

        // The reset page cannot work without a token.
        if (!token) {
            setErrorMessage(
                "The password reset link is missing its token or is invalid."
            );
            return;
        }

        // Matches the backend [MinLength(8)] validation.
        if (newPassword.length < 8) {
            setErrorMessage(
                "Your new password must contain at least 8 characters."
            );
            return;
        }

        // Matches the backend [MaxLength(100)] validation.
        if (newPassword.length > 100) {
            setErrorMessage(
                "Your new password cannot exceed 100 characters."
            );
            return;
        }

        // Matches [Compare(nameof(NewPassword))].
        if (newPassword !== confirmPassword) {
            setErrorMessage("The passwords do not match.");
            return;
        }

        try {
            setIsSubmitting(true);

            const response = await fetch(
                "https://localhost:7000/api/Auth/reset-password",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        token,
                        newPassword,
                        confirmPassword,
                    }),
                }
            );

            if (!response.ok) {
                let message = "Unable to reset your password.";

                try {
                    const errorData = await response.json();

                    message =
                        errorData.message ??
                        errorData.title ??
                        message;
                } catch {
                    // Keeps the default error message if the response
                    // does not contain JSON.
                }

                throw new Error(message);
            }

            setSuccessMessage(
                "Your password has been reset successfully."
            );

            setNewPassword("");
            setConfirmPassword("");

            // Redirects the user to the sign-in page after two seconds.
            window.setTimeout(() => {
                navigate("/auth");
            }, 2000);
        } catch (error) {
            if (error instanceof Error) {
                setErrorMessage(error.message);
            } else {
                setErrorMessage(
                    "An unexpected error occurred. Please try again."
                );
            }
        } finally {
            setIsSubmitting(false);
        }
    }

    return (
        <main className="reset-password-page">
            <section className="reset-password-presentation">
                <div className="reset-password-logo">
                    <div className="reset-password-logo-symbol">CM</div>

                    <div className="reset-password-logo-name">
                        Career<span>Match</span>
                    </div>
                </div>

                <div className="reset-password-message">
                    <p className="reset-password-eyebrow">
                        Secure your account
                    </p>

                    <h1>
                        Create your
                        <span>new password.</span>
                    </h1>

                    <div className="reset-password-title-line">
                        <span />
                        <span />
                    </div>

                    <p className="reset-password-description">
                        Choose a strong password that you have not previously
                        used for your CareerMatch account.
                    </p>
                </div>

                <div className="reset-password-portal" aria-hidden="true">
                    <div className="reset-password-portal-glow" />

                    <div className="reset-password-lock">
                        <div className="reset-password-lock-hook" />
                        <div className="reset-password-lock-body">
                            <span>CM</span>
                            <div className="reset-password-keyhole" />
                        </div>
                    </div>
                </div>
            </section>

            <section className="reset-password-form-section">
                <div className="reset-password-card">
                    <header className="reset-password-card-heading">
                        <div className="reset-password-card-icon">
                            <div className="reset-password-card-lock-hook" />
                            <div className="reset-password-card-lock-body" />
                        </div>

                        <div>
                            <h2>Reset password</h2>
                            <p>
                                Enter and confirm your new account password.
                            </p>
                        </div>
                    </header>

                    <form
                        className="reset-password-form"
                        onSubmit={handleSubmit}
                    >
                        <div className="reset-form-group">
                            <label htmlFor="new-password">
                                New password
                            </label>

                            <div className="reset-input-container">
                                <span
                                    className="reset-input-icon"
                                    aria-hidden="true"
                                >
                                    🔒
                                </span>

                                <input
                                    id="new-password"
                                    type={
                                        showNewPassword
                                            ? "text"
                                            : "password"
                                    }
                                    value={newPassword}
                                    onChange={(event) =>
                                        setNewPassword(event.target.value)
                                    }
                                    placeholder="Enter your new password"
                                    minLength={8}
                                    maxLength={100}
                                    autoComplete="new-password"
                                    required
                                />

                                <button
                                    className="reset-password-visibility-button"
                                    type="button"
                                    onClick={() =>
                                        setShowNewPassword(
                                            (currentValue) =>
                                                !currentValue
                                        )
                                    }
                                    aria-label={
                                        showNewPassword
                                            ? "Hide new password"
                                            : "Show new password"
                                    }
                                >
                                    {showNewPassword ? "◉" : "◎"}
                                </button>
                            </div>

                            <small>
                                Your password must contain at least 8
                                characters.
                            </small>
                        </div>

                        <div className="reset-form-group">
                            <label htmlFor="confirm-password">
                                Confirm password
                            </label>

                            <div className="reset-input-container">
                                <span
                                    className="reset-input-icon"
                                    aria-hidden="true"
                                >
                                    ✓
                                </span>

                                <input
                                    id="confirm-password"
                                    type={
                                        showConfirmPassword
                                            ? "text"
                                            : "password"
                                    }
                                    value={confirmPassword}
                                    onChange={(event) =>
                                        setConfirmPassword(
                                            event.target.value
                                        )
                                    }
                                    placeholder="Confirm your new password"
                                    minLength={8}
                                    maxLength={100}
                                    autoComplete="new-password"
                                    required
                                />

                                <button
                                    className="reset-password-visibility-button"
                                    type="button"
                                    onClick={() =>
                                        setShowConfirmPassword(
                                            (currentValue) =>
                                                !currentValue
                                        )
                                    }
                                    aria-label={
                                        showConfirmPassword
                                            ? "Hide confirmed password"
                                            : "Show confirmed password"
                                    }
                                >
                                    {showConfirmPassword ? "◉" : "◎"}
                                </button>
                            </div>
                        </div>

                        {errorMessage && (
                            <p
                                className="reset-password-message-box error"
                                role="alert"
                            >
                                {errorMessage}
                            </p>
                        )}

                        {successMessage && (
                            <p
                                className="reset-password-message-box success"
                                role="status"
                            >
                                {successMessage}
                            </p>
                        )}

                        <button
                            className="reset-password-submit-button"
                            type="submit"
                            disabled={isSubmitting}
                        >
                            <span>
                                {isSubmitting
                                    ? "Resetting password..."
                                    : "Create new password"}
                            </span>

                            <span
                                className="reset-password-submit-arrow"
                                aria-hidden="true"
                            >
                                →
                            </span>
                        </button>
                    </form>

                    <button
                        className="reset-password-back-button"
                        type="button"
                        onClick={() => navigate("/auth")}
                    >
                        ← Back to Sign In
                    </button>
                </div>
            </section>
        </main>
    );
}

export default ResetPasswordPage;