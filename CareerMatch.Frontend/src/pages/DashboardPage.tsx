import { Link } from "react-router-dom";
import dashboardVisual from "../assets/dashboard/career-dashboard-visual.png";
import "./DashboardPage.css";

const dashboardFeatures = [
    {
        icon: "📄",
        title: "Refine Your CV",
        description:
            "Improve your CV by aligning it with job descriptions and highlighting your strengths.",
    },
    {
        icon: "✉️",
        title: "Generate Cover Letters",
        description:
            "Create personalized cover letters that make a strong impression.",
    },
    {
        icon: "❔",
        title: "Interview Questions",
        description:
            "Get interview questions based on the job you are applying for.",
    },
    {
        icon: "💼",
        title: "Jobs You Applied To",
        description:
            "Manage all the jobs you have applied to in one place.",
    },
    {
        icon: "🔖",
        title: "Saved Jobs",
        description:
            "Save jobs you like and never miss the right opportunity.",
    },
    {
        icon: "🔍",
        title: "Search Jobs",
        description:
            "Explore available jobs and find the opportunity that fits you best.",
    },
];

function DashboardPage() {
    return (
        <main className="dashboard-page">
            <header className="dashboard-header">
                <div className="dashboard-logo">
                    <span className="logo-symbol">CM</span>
                    <span className="logo-name">CareerMatch</span>
                </div>
            </header>

            <section className="hero-section">
                <div className="hero-content">
                    <h1 className="hero-title">
                        Are you a
                        <span>Job Seeker?</span>
                    </h1>

                    <p className="hero-description">
                        We connect your skills and ambitions with opportunities
                        that help you grow and move forward in your career.
                    </p>

                    <Link to="/auth" className="get-started-button">
                        <span>Get Started</span>
                        <span className="button-arrow">→</span>
                    </Link>
                </div>

                <div className="hero-visual">
                    <img
                        src={dashboardVisual}
                        className="hero-visual-image"
                        alt="CareerMatch job dashboard displayed on a laptop"
                    />
                </div>
            </section>

            <section className="features-section">
                <h2 className="features-title">
                    Here&apos;s what we offer for you
                </h2>

                <div className="features-grid">
                    {dashboardFeatures.map((feature) => (
                        <article
                            className="feature-card"
                            key={feature.title}
                        >
                            <div className="feature-icon">
                                {feature.icon}
                            </div>

                            <h3>{feature.title}</h3>

                            <p>{feature.description}</p>
                        </article>
                    ))}
                </div>
            </section>
        </main>
    );
}

export default DashboardPage;