import { useState, type FormEvent } from "react";
import {
    useNavigate,
    useSearchParams,
} from "react-router-dom";
import { AxiosError } from "axios";
import api from "../services/api";
import "./ResetPasswordPage.css";

function ResetPasswordPage() {
    const [searchParams] = useSearchParams();

    const navigate = useNavigate();

    const token = searchParams.get("token") ?? "";

    const [newPassword, setNewPassword] = useState("");

    const [confirmPassword, setConfirmPassword] = useState("");

    const [showNewPassword, setShowNewPassword] = useState(false);

    const [showConfirmPassword, setShowConfirmPassword] =
        useState(false);

    const [errorMessage, setErrorMessage] = useState("");

    const [successMessage, setSuccessMessage] = useState("");

    const [isSubmitting, setIsSubmitting] = useState(false);

    async function handleSubmit(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        setErrorMessage("");
        setSuccessMessage("");

        if (!token) {
            setErrorMessage(
                "The password reset link is missing its token or is invalid."
            );

            return;
        }

        if (newPassword.length < 8) {
            setErrorMessage(
                "Your new password must contain at least 8 characters."
            );

            return;
        }

        if (newPassword.length > 100) {
            setErrorMessage(
                "Your new password cannot exceed 100 characters."
            );

            return;
        }

        if (newPassword !== confirmPassword) {
            setErrorMessage("The passwords do not match.");

            return;
        }

        try {
            setIsSubmitting(true);

            const response = await api.post(
                "/Auth/reset-password",
                {
                    token,
                    newPassword,
                    confirmPassword,
                }
            );

            setSuccessMessage(
                typeof response.data === "string"
                    ? response.data
                    : "Your password has been reset successfully."
            );

            setNewPassword("");
            setConfirmPassword("");

            window.setTimeout(() => {
                navigate("/auth");
            }, 2000);
        } catch (error) {
            if (error instanceof AxiosError) {
                const backendResponse = error.response?.data;

                if (typeof backendResponse === "string") {
                    setErrorMessage(backendResponse);
                } else if (
                    backendResponse &&
                    typeof backendResponse === "object" &&
                    "message" in backendResponse
                ) {
                    setErrorMessage(
                        String(backendResponse.message)
                    );
                } else if (
                    backendResponse &&
                    typeof backendResponse === "object" &&
                    "title" in backendResponse
                ) {
                    setErrorMessage(
                        String(backendResponse.title)
                    );
                } else {
                    setErrorMessage(
                        "Unable to reset your password. The link may be invalid or expired."
                    );
                }
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
                    <div className="reset-password-logo-symbol">
                        CM
                    </div>

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
                        Choose a strong password that you have not
                        previously used for your CareerMatch account.
                    </p>
                </div>

                <div
                    className="reset-password-portal"
                    aria-hidden="true"
                >
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
                                Enter and confirm your new account
                                password.
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
                                        setNewPassword(
                                            event.target.value
                                        )
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