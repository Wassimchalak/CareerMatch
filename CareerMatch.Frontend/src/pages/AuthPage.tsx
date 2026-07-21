import { useState, type FormEvent } from "react";
import "./AuthPage.css";

type AuthMode = "signin" | "signup";

function AuthPage() {
    const [authMode, setAuthMode] = useState<AuthMode>("signin");

    const [fullName, setFullName] = useState("");
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(false);

    const isSignUp = authMode === "signup";

    function switchAuthMode(mode: AuthMode) {
        setAuthMode(mode);

        setFullName("");
        setEmail("");
        setPassword("");
        setShowPassword(false);
        setRememberMe(false);
    }

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (isSignUp) {
            const signupData = {
                fullName,
                email,
                password,
            };

            console.log("Signup data:", signupData);
            return;
        }

        const loginData = {
            email,
            password,
            rememberMe,
        };

        console.log("Login data:", loginData);
    }

    return (
        <main className="auth-page">
            <section className="auth-presentation">
                <header className="auth-logo">
                    <span className="auth-logo-symbol">CM</span>

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
                            <span className="door-logo">CM</span>
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
                                authMode === "signin" ? "active" : ""
                            }`}
                            onClick={() => switchAuthMode("signin")}
                        >
                            Sign In
                        </button>

                        <button
                            type="button"
                            className={`auth-tab ${
                                authMode === "signup" ? "active" : ""
                            }`}
                            onClick={() => switchAuthMode("signup")}
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
                                        onChange={(event) =>
                                            setFullName(
                                                event.target.value,
                                            )
                                        }
                                        required
                                        maxLength={100}
                                        autoComplete="name"
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
                                    onChange={(event) =>
                                        setEmail(event.target.value)
                                    }
                                    required
                                    maxLength={150}
                                    autoComplete="email"
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
                                    onChange={(event) =>
                                        setPassword(
                                            event.target.value,
                                        )
                                    }
                                    required
                                    minLength={6}
                                    autoComplete={
                                        isSignUp
                                            ? "new-password"
                                            : "current-password"
                                    }
                                />

                                <button
                                    type="button"
                                    className="password-visibility-button"
                                    onClick={() =>
                                        setShowPassword(
                                            (currentValue) =>
                                                !currentValue,
                                        )
                                    }
                                    aria-label={
                                        showPassword
                                            ? "Hide password"
                                            : "Show password"
                                    }
                                >
                                    {showPassword ? "◉" : "◎"}
                                </button>
                            </div>
                        </div>

                        {!isSignUp && (
                            <div className="auth-options">
                                <label className="remember-option">
                                    <input
                                        type="checkbox"
                                        checked={rememberMe}
                                        onChange={(event) =>
                                            setRememberMe(
                                                event.target.checked,
                                            )
                                        }
                                    />

                                    <span>Remember me</span>
                                </label>

                                <button
                                    type="button"
                                    className="forgot-password-button"
                                >
                                    Forgot password?
                                </button>
                            </div>
                        )}

                        <button
                            type="submit"
                            className="auth-submit-button"
                        >
                            <span>
                                {isSignUp
                                    ? "Create Account"
                                    : "Sign In"}
                            </span>

                            <span className="submit-arrow">→</span>
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
                                        : "signup",
                                )
                            }
                        >
                            {isSignUp ? "Sign in" : "Sign up"}
                        </button>
                    </p>
                </div>
            </section>
        </main>
    );
}

export default AuthPage;