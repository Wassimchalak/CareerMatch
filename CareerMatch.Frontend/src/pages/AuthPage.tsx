import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import api from "../services/api";
import "./AuthPage.css";

type AuthMode = "signin" | "signup";
interface AuthResponse {
    token?: string;
    userId?: number;
    fullName?: string;
    email?: string;
    expiresAt?: string;
}

interface TokenPayload {
    name?: string;
    fullName?: string;
    email?: string;

    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"?:
        string;

    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"?:
        string;
}

function readTokenPayload(
    token: string
): TokenPayload | null {
    try {
        const tokenParts = token.split(".");

        if (tokenParts.length < 2) {
            return null;
        }

        const payload = tokenParts[1]
            .replace(/-/g, "+")
            .replace(/_/g, "/");

        const paddedPayload = payload.padEnd(
            Math.ceil(payload.length / 4) * 4,
            "="
        );

        return JSON.parse(
            decodeURIComponent(
                Array.from(
                    atob(paddedPayload)
                )
                    .map(
                        (character) =>
                            `%${character
                                .charCodeAt(0)
                                .toString(16)
                                .padStart(2, "0")}`
                    )
                    .join("")
            )
        ) as TokenPayload;
    } catch {
        return null;
    }
}

function saveAuthenticationData(
    responseData: AuthResponse,
    enteredEmail: string,
    enteredFullName?: string
) {
    const token =
        responseData.token || "";

    const tokenPayload = token
        ? readTokenPayload(token)
        : null;

    const resolvedFullName =
        responseData.fullName ||
        enteredFullName ||
        tokenPayload?.fullName ||
        tokenPayload?.name ||
        tokenPayload?.[
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
        ] ||
        "";

    const resolvedEmail =
        responseData.email ||
        tokenPayload?.email ||
        tokenPayload?.[
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
        ] ||
        enteredEmail;

    if (token) {
        localStorage.setItem(
            "token",
            token
        );
    }

    if (responseData.userId !== undefined) {
        localStorage.setItem(
            "userId",
            responseData.userId.toString()
        );
    }

    if (resolvedFullName) {
        localStorage.setItem(
            "fullName",
            resolvedFullName
        );
    }

    if (resolvedEmail) {
        localStorage.setItem(
            "email",
            resolvedEmail
        );
    }

    if (responseData.expiresAt) {
        localStorage.setItem(
            "expiresAt",
            responseData.expiresAt
        );
    }
}
function AuthPage() {
    const navigate = useNavigate();

    const [authMode, setAuthMode] =
        useState<AuthMode>("signin");

    const [fullName, setFullName] =
        useState("");

    const [email, setEmail] =
        useState("");

    const [password, setPassword] =
        useState("");

    const [showPassword, setShowPassword] =
        useState(false);

    const [isSubmitting, setIsSubmitting] =
        useState(false);

    const [errorMessage, setErrorMessage] =
        useState("");

    const isSignUp =
        authMode === "signup";

    function switchAuthMode(
        mode: AuthMode
    ) {
        setAuthMode(mode);
        setFullName("");
        setEmail("");
        setPassword("");
        setShowPassword(false);
        setErrorMessage("");
    }

   async function handleSubmit(
    event: FormEvent<HTMLFormElement>
) {
    event.preventDefault();

    setIsSubmitting(true);
    setErrorMessage("");

    try {
        if (isSignUp) {
            const signupData = {
                fullName: fullName.trim(),
                email: email.trim(),
                password,
            };

            const response =
                await api.post<AuthResponse>(
                    "/Auth/register",
                    signupData
                );

            saveAuthenticationData(
                response.data,
                email.trim(),
                fullName.trim()
            );

            navigate("/dashboard");
            return;
        }

        const loginData = {
            email: email.trim(),
            password,
        };

        const response =
            await api.post<AuthResponse>(
                "/Auth/login",
                loginData
            );

        saveAuthenticationData(
            response.data,
            email.trim()
        );

        navigate("/dashboard");
    } catch (error: unknown) {
        console.error(
            isSignUp
                ? "Signup failed:"
                : "Login failed:",
            error
        );

        let backendMessage = "";

        if (
            typeof error === "object" &&
            error !== null &&
            "response" in error
        ) {
            const responseError = error as {
                response?: {
                    data?:
                        | string
                        | {
                              message?: string;
                          };
                };
            };

            const responseData =
                responseError.response?.data;

            if (typeof responseData === "string") {
                backendMessage = responseData;
            } else if (
                responseData &&
                typeof responseData.message ===
                    "string"
            ) {
                backendMessage =
                    responseData.message;
            }
        }

        setErrorMessage(
            backendMessage.trim()
                ? backendMessage
                : isSignUp
                  ? "Account creation failed. Please try again."
                  : "Login failed. Check your email and password."
        );
    } finally {
        setIsSubmitting(false);
    }
}

    return (
        <main className="auth-page">
            <section className="auth-presentation">
                <header className="auth-logo">
                    <span className="auth-logo-symbol">
                        CM
                    </span>

                    <span className="auth-logo-name">
                        Career<span>Match</span>
                    </span>
                </header>

                <div className="auth-message">
                    <h1>
                        Open the door
                        <span>to your</span>
                        <strong>future.</strong>
                    </h1>

                    <div className="auth-title-line">
                        <span />
                        <span />
                    </div>

                    <p className="auth-tagline">
                        <span>Where talent</span>
                        <span>meets</span>
                        <strong>opportunity.</strong>
                    </p>
                </div>

                <div className="career-door-scene">
                    <div className="door-glow" />

                    <div className="career-door">
                        <div className="door-inner">
                            <span className="door-logo">
                                CM
                            </span>

                            <span className="door-handle" />
                        </div>
                    </div>

                    <div className="door-platform">
                        <div className="platform-step platform-step-one" />
                        <div className="platform-step platform-step-two" />
                        <div className="platform-step platform-step-three" />
                    </div>
                </div>
            </section>

            <section className="auth-form-section">
                <div className="auth-card">
                    <div className="auth-tabs">
                        <button
                            type="button"
                            className={`auth-tab ${
                                authMode ===
                                "signin"
                                    ? "active"
                                    : ""
                            }`}
                            onClick={() =>
                                switchAuthMode(
                                    "signin"
                                )
                            }
                            disabled={isSubmitting}
                        >
                            Sign In
                        </button>

                        <button
                            type="button"
                            className={`auth-tab ${
                                authMode ===
                                "signup"
                                    ? "active"
                                    : ""
                            }`}
                            onClick={() =>
                                switchAuthMode(
                                    "signup"
                                )
                            }
                            disabled={isSubmitting}
                        >
                            Sign Up
                        </button>
                    </div>

                    <div className="auth-card-heading">
                        <div
                            className="auth-user-icon"
                            aria-hidden="true"
                        >
                            <span className="user-head" />
                            <span className="user-body" />
                        </div>

                        <div>
                            <h2>
                                {isSignUp
                                    ? "Create your account"
                                    : "Welcome back!"}
                            </h2>

                            <p>
                                {isSignUp
                                    ? "Start building your next career opportunity."
                                    : "Glad to see you again. Let’s continue your journey."}
                            </p>
                        </div>
                    </div>

                    <form
                        className="auth-form"
                        onSubmit={handleSubmit}
                    >
                        {isSignUp && (
                            <div className="form-group">
                                <label htmlFor="fullName">
                                    Full name
                                </label>

                                <div className="input-container">
                                    <span
                                        className="input-icon"
                                        aria-hidden="true"
                                    >
                                        ♙
                                    </span>

                                    <input
                                        id="fullName"
                                        name="fullName"
                                        type="text"
                                        placeholder="Enter your full name"
                                        value={fullName}
                                        onChange={(
                                            event
                                        ) =>
                                            setFullName(
                                                event
                                                    .target
                                                    .value
                                            )
                                        }
                                        required
                                        maxLength={
                                            100
                                        }
                                        autoComplete="name"
                                        disabled={
                                            isSubmitting
                                        }
                                    />
                                </div>
                            </div>
                        )}

                        <div className="form-group">
                            <label htmlFor="email">
                                Email address
                            </label>

                            <div className="input-container">
                                <span
                                    className="input-icon"
                                    aria-hidden="true"
                                >
                                    ✉
                                </span>

                                <input
                                    id="email"
                                    name="email"
                                    type="email"
                                    placeholder="Enter your email"
                                    value={email}
                                    onChange={(
                                        event
                                    ) =>
                                        setEmail(
                                            event.target
                                                .value
                                        )
                                    }
                                    required
                                    maxLength={150}
                                    autoComplete="email"
                                    disabled={
                                        isSubmitting
                                    }
                                />
                            </div>
                        </div>

                        <div className="form-group">
                            <label htmlFor="password">
                                Password
                            </label>

                            <div className="input-container">
                                <span
                                    className="input-icon"
                                    aria-hidden="true"
                                >
                                    ♙
                                </span>

                                <input
                                    id="password"
                                    name="password"
                                    type={
                                        showPassword
                                            ? "text"
                                            : "password"
                                    }
                                    placeholder="Enter your password"
                                    value={password}
                                    onChange={(
                                        event
                                    ) =>
                                        setPassword(
                                            event.target
                                                .value
                                        )
                                    }
                                    required
                                    minLength={6}
                                    autoComplete={
                                        isSignUp
                                            ? "new-password"
                                            : "current-password"
                                    }
                                    disabled={
                                        isSubmitting
                                    }
                                />

                                <button
                                    type="button"
                                    className="password-visibility-button"
                                    onClick={() =>
                                        setShowPassword(
                                            (
                                                currentValue
                                            ) =>
                                                !currentValue
                                        )
                                    }
                                    aria-label={
                                        showPassword
                                            ? "Hide password"
                                            : "Show password"
                                    }
                                    disabled={
                                        isSubmitting
                                    }
                                >
                                    {showPassword
                                        ? "◉"
                                        : "◎"}
                                </button>
                            </div>
                        </div>

                        {!isSignUp && (
                            <div className="auth-options">
                                <button
                                    type="button"
                                    className="forgot-password-button"
                                    onClick={() =>
                                        navigate(
                                            "/forgot-password"
                                        )
                                    }
                                    disabled={
                                        isSubmitting
                                    }
                                >
                                    Forgot password?
                                </button>
                            </div>
                        )}

                        {errorMessage && (
                            <p
                                className="auth-error-message"
                                role="alert"
                            >
                                {errorMessage}
                            </p>
                        )}

                        <button
                            type="submit"
                            className="auth-submit-button"
                            disabled={isSubmitting}
                        >
                            <span>
                                {isSubmitting
                                    ? isSignUp
                                        ? "Creating account..."
                                        : "Signing in..."
                                    : isSignUp
                                      ? "Create Account"
                                      : "Sign In"}
                            </span>

                            <span className="submit-arrow">
                                →
                            </span>
                        </button>
                    </form>

                    <div className="auth-card-divider">
                        <span />
                        <strong>✦</strong>
                        <span />
                    </div>

                    <p className="auth-switch-message">
                        {isSignUp
                            ? "Already have an account?"
                            : "Don’t have an account?"}

                        <button
                            type="button"
                            onClick={() =>
                                switchAuthMode(
                                    isSignUp
                                        ? "signin"
                                        : "signup"
                                )
                            }
                            disabled={isSubmitting}
                        >
                            {isSignUp
                                ? "Sign in"
                                : "Sign up"}
                        </button>
                    </p>
                </div>
            </section>
        </main>
    );
}

export default AuthPage;