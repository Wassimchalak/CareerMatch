import {
    useState,
    type CSSProperties,
    type FormEvent,
} from "react";
import { Link } from "react-router-dom";
import axios from "axios";
import api from "../services/api";

function ForgotPasswordPage() {
    const [email, setEmail] =
        useState("");

    const [message, setMessage] =
        useState("");

    const [isSuccess, setIsSuccess] =
        useState(false);

    const [loading, setLoading] =
        useState(false);

    async function handleSubmit(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        setMessage("");
        setIsSuccess(false);

        try {
            setLoading(true);

            const response =
                await api.post<string>(
                    "/Auth/forgot-password",
                    {
                        email: email.trim(),
                    }
                );

            setMessage(
                typeof response.data === "string"
                    ? response.data
                    : "A password reset link was sent to your email."
            );

            setIsSuccess(true);
        } catch (error: unknown) {
            setIsSuccess(false);

            if (axios.isAxiosError(error)) {
                const responseData =
                    error.response?.data;

                if (
                    typeof responseData ===
                    "string"
                ) {
                    setMessage(responseData);
                } else if (
                    responseData &&
                    typeof responseData ===
                        "object" &&
                    "message" in responseData &&
                    typeof responseData.message ===
                        "string"
                ) {
                    setMessage(
                        responseData.message
                    );
                } else {
                    setMessage(
                        "The reset link could not be sent."
                    );
                }
            } else {
                setMessage(
                    "Something went wrong. Please try again."
                );
            }
        } finally {
            setLoading(false);
        }
    }

    const styles: Record<
        string,
        CSSProperties
    > = {
        page: {
            position: "relative",
            minHeight: "100vh",
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            overflow: "hidden",
            padding: "32px 20px",
            boxSizing: "border-box",
            fontFamily:
                "Inter, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
            background:
                "linear-gradient(135deg, #07051c 0%, #160934 45%, #24105a 100%)",
        },

        glowTop: {
            position: "absolute",
            top: "-180px",
            right: "-120px",
            width: "460px",
            height: "460px",
            borderRadius: "50%",
            background:
                "rgba(168, 85, 247, 0.25)",
            filter: "blur(110px)",
            pointerEvents: "none",
        },

        glowBottom: {
            position: "absolute",
            bottom: "-200px",
            left: "-120px",
            width: "500px",
            height: "500px",
            borderRadius: "50%",
            background:
                "rgba(124, 58, 237, 0.3)",
            filter: "blur(120px)",
            pointerEvents: "none",
        },

        glowCenter: {
            position: "absolute",
            top: "35%",
            left: "50%",
            width: "300px",
            height: "300px",
            borderRadius: "50%",
            background:
                "rgba(236, 72, 153, 0.12)",
            filter: "blur(100px)",
            transform:
                "translate(-50%, -50%)",
            pointerEvents: "none",
        },

        form: {
            position: "relative",
            zIndex: 1,
            width: "100%",
            maxWidth: "440px",
            padding: "42px",
            borderRadius: "26px",
            border:
                "1px solid rgba(255, 255, 255, 0.1)",
            background:
                "linear-gradient(145deg, rgba(27, 18, 61, 0.9), rgba(12, 9, 34, 0.86))",
            backdropFilter: "blur(24px)",
            WebkitBackdropFilter:
                "blur(24px)",
            boxShadow:
                "0 30px 80px rgba(0, 0, 0, 0.5), 0 0 50px rgba(139, 92, 246, 0.15)",
            boxSizing: "border-box",
        },

        logoRow: {
            display: "flex",
            alignItems: "center",
            gap: "12px",
            marginBottom: "34px",
        },

        logo: {
            width: "46px",
            height: "46px",
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            flexShrink: 0,
            borderRadius: "14px",
            color: "#ffffff",
            fontWeight: 800,
            fontSize: "15px",
            letterSpacing: "-0.5px",
            background:
                "linear-gradient(135deg, #a855f7 0%, #7c3aed 55%, #5b21b6 100%)",
            boxShadow:
                "0 12px 28px rgba(139, 92, 246, 0.4)",
        },

        brandText: {
            display: "grid",
            gap: "2px",
        },

        brandName: {
            margin: 0,
            color: "#ffffff",
            fontSize: "18px",
            fontWeight: 750,
        },

        brandRole: {
            color: "#a7a1c5",
            fontSize: "12px",
        },

        iconWrapper: {
            width: "68px",
            height: "68px",
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            marginBottom: "24px",
            borderRadius: "20px",
            border:
                "1px solid rgba(192, 132, 252, 0.28)",
            color: "#ffffff",
            fontSize: "29px",
            background:
                "linear-gradient(145deg, rgba(168, 85, 247, 0.25), rgba(109, 40, 217, 0.18))",
            boxShadow:
                "0 15px 35px rgba(124, 58, 237, 0.25)",
        },

        heading: {
            margin: 0,
            color: "#ffffff",
            fontSize: "31px",
            lineHeight: 1.2,
            letterSpacing: "-0.8px",
            fontWeight: 750,
        },

        description: {
            margin: "14px 0 28px",
            color: "#aaa5c4",
            fontSize: "14px",
            lineHeight: 1.7,
        },

        label: {
            display: "block",
            marginBottom: "9px",
            color: "#e9e7f4",
            fontSize: "13px",
            fontWeight: 650,
        },

        input: {
            width: "100%",
            minHeight: "52px",
            padding: "0 17px",
            borderRadius: "14px",
            border:
                "1px solid rgba(255, 255, 255, 0.12)",
            outline: "none",
            color: "#ffffff",
            background:
                "rgba(255, 255, 255, 0.055)",
            fontSize: "15px",
            boxSizing: "border-box",
            transition:
                "border-color 0.2s ease, box-shadow 0.2s ease, background 0.2s ease",
        },

        button: {
            width: "100%",
            minHeight: "52px",
            marginTop: "22px",
            padding: "0 18px",
            border: "none",
            borderRadius: "14px",
            color: "#ffffff",
            fontSize: "15px",
            fontWeight: 750,
            cursor: loading
                ? "not-allowed"
                : "pointer",
            background:
                "linear-gradient(135deg, #a855f7 0%, #7c3aed 50%, #6d28d9 100%)",
            boxShadow:
                "0 14px 32px rgba(124, 58, 237, 0.38)",
            opacity: loading ? 0.72 : 1,
            transition:
                "transform 0.2s ease, box-shadow 0.2s ease, opacity 0.2s ease",
        },

        message: {
            marginTop: "20px",
            padding: "14px 16px",
            borderRadius: "13px",
            color: isSuccess
                ? "#a7f3d0"
                : "#fda4af",
            border: isSuccess
                ? "1px solid rgba(52, 211, 153, 0.3)"
                : "1px solid rgba(251, 113, 133, 0.3)",
            background: isSuccess
                ? "rgba(16, 185, 129, 0.1)"
                : "rgba(244, 63, 94, 0.1)",
            fontSize: "13px",
            lineHeight: 1.6,
        },

        divider: {
            height: "1px",
            margin: "28px 0 22px",
            background:
                "linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.12), transparent)",
        },

        backWrapper: {
            textAlign: "center",
        },

        backLink: {
            color: "#c4b5fd",
            textDecoration: "none",
            fontSize: "14px",
            fontWeight: 650,
        },
    };

    return (
        <main style={styles.page}>
            <div style={styles.glowTop} />
            <div style={styles.glowBottom} />
            <div style={styles.glowCenter} />

            <form
                onSubmit={handleSubmit}
                style={styles.form}
            >
                <div style={styles.logoRow}>
                    <div style={styles.logo}>
                        CM
                    </div>

                    <div style={styles.brandText}>
                        <p style={styles.brandName}>
                            CareerMatch
                        </p>

                        <span
                            style={
                                styles.brandRole
                            }
                        >
                            Job Seeker Platform
                        </span>
                    </div>
                </div>

                <div
                    style={
                        styles.iconWrapper
                    }
                    aria-hidden="true"
                >
                    ✉
                </div>

                <h1 style={styles.heading}>
                    Forgot your password?
                </h1>

                <p
                    style={
                        styles.description
                    }
                >
                    Enter the email address
                    linked to your CareerMatch
                    account. We will send you
                    a secure link to reset your
                    password.
                </p>

                <label
                    htmlFor="forgot-password-email"
                    style={styles.label}
                >
                    Email address
                </label>

                <input
                    id="forgot-password-email"
                    type="email"
                    placeholder="name@example.com"
                    value={email}
                    onChange={(event) =>
                        setEmail(
                            event.target.value
                        )
                    }
                    required
                    autoComplete="email"
                    disabled={loading}
                    style={styles.input}
                    onFocus={(event) => {
                        event.currentTarget.style.borderColor =
                            "rgba(168, 85, 247, 0.85)";

                        event.currentTarget.style.boxShadow =
                            "0 0 0 4px rgba(124, 58, 237, 0.16)";

                        event.currentTarget.style.background =
                            "rgba(255, 255, 255, 0.075)";
                    }}
                    onBlur={(event) => {
                        event.currentTarget.style.borderColor =
                            "rgba(255, 255, 255, 0.12)";

                        event.currentTarget.style.boxShadow =
                            "none";

                        event.currentTarget.style.background =
                            "rgba(255, 255, 255, 0.055)";
                    }}
                />

                <button
                    type="submit"
                    disabled={loading}
                    style={styles.button}
                    onMouseEnter={(event) => {
                        if (!loading) {
                            event.currentTarget.style.transform =
                                "translateY(-2px)";

                            event.currentTarget.style.boxShadow =
                                "0 18px 38px rgba(124, 58, 237, 0.48)";
                        }
                    }}
                    onMouseLeave={(event) => {
                        event.currentTarget.style.transform =
                            "translateY(0)";

                        event.currentTarget.style.boxShadow =
                            "0 14px 32px rgba(124, 58, 237, 0.38)";
                    }}
                >
                    {loading
                        ? "Sending reset link..."
                        : "Send Reset Link"}
                </button>

                {message && (
                    <div
                        style={styles.message}
                        role={
                            isSuccess
                                ? "status"
                                : "alert"
                        }
                    >
                        {message}
                    </div>
                )}

                <div style={styles.divider} />

                <div
                    style={
                        styles.backWrapper
                    }
                >
                    <Link
                        to="/"
                        style={
                            styles.backLink
                        }
                    >
                        ← Back to Sign In
                    </Link>
                </div>
            </form>
        </main>
    );
}

export default ForgotPasswordPage;