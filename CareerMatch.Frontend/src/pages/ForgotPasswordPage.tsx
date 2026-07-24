import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import api from "../services/api";

function ForgotPasswordPage() {
    const [email, setEmail] = useState("");
    const [message, setMessage] = useState("");
    const [loading, setLoading] = useState(false);

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        try {
            setLoading(true);

            const response = await api.post("/Auth/forgot-password", {
                email,
            });

            setMessage(response.data);
        } catch (error: any) {
            if (error.response) {
                setMessage(error.response.data);
            } else {
                setMessage("Something went wrong.");
            }
        } finally {
            setLoading(false);
        }
    }

    return (
        <div
            style={{
                minHeight: "100vh",
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                background: "#111827",
            }}
        >
            <form
                onSubmit={handleSubmit}
                style={{
                    background: "white",
                    padding: "40px",
                    borderRadius: "15px",
                    width: "400px",
                }}
            >
                <h2>Forgot Password</h2>

                <p>
                    Enter your email address and we'll send you a reset link.
                </p>

                <input
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    style={{
                        width: "100%",
                        padding: "12px",
                        marginTop: "20px",
                        marginBottom: "20px",
                    }}
                />

                <button
                    type="submit"
                    disabled={loading}
                    style={{
                        width: "100%",
                        padding: "12px",
                    }}
                >
                    {loading ? "Sending..." : "Send Reset Link"}
                </button>

                {message && (
                    <p
                        style={{
                            marginTop: "20px",
                            color: "green",
                        }}
                    >
                        {message}
                    </p>
                )}

                <div style={{ marginTop: "20px" }}>
                    <Link to="/">← Back to Sign In</Link>
                </div>
            </form>
        </div>
    );
}

export default ForgotPasswordPage;