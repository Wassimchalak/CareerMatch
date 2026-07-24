import { useEffect, useMemo, useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import axios from "axios";
import api from "../services/api";
import "./AppliedJobsPage.css";

interface AppliedJobResponse {
    applicationId: number;
    jobId: number;
    title: string;
    companyName: string;
    country: string;
    city: string | null;
    jobUrl: string;
    applicationStatus: string;
    appliedAt: string;
    matchScore: number | null;
    matchExplanation: string | null;
    recommendation: string | null;
}

function AppliedJobsPage() {
    const navigate = useNavigate();

    const [sidebarOpen, setSidebarOpen] =
        useState(false);

    const [appliedJobs, setAppliedJobs] =
        useState<AppliedJobResponse[]>([]);

    const [loadingAppliedJobs, setLoadingAppliedJobs] =
        useState(true);

    const [
        removingApplicationIds,
        setRemovingApplicationIds,
    ] = useState<Set<number>>(new Set());

    const [successMessage, setSuccessMessage] =
        useState("");

    const [errorMessage, setErrorMessage] =
        useState("");

    const fullName =
        localStorage.getItem("fullName") ||
        "Job Seeker";

    const email =
        localStorage.getItem("email") ||
        "";

    const userInitials = useMemo(() => {
        const initials = fullName
            .trim()
            .split(/\s+/)
            .slice(0, 2)
            .map((part) => part.charAt(0))
            .join("")
            .toUpperCase();

        return initials || "JS";
    }, [fullName]);

    const getErrorMessage = (
        error: unknown,
        fallbackMessage: string
    ) => {
        if (axios.isAxiosError(error)) {
            const responseData =
                error.response?.data;

            if (typeof responseData === "string") {
                return responseData;
            }

            if (
                responseData &&
                typeof responseData === "object"
            ) {
                if (
                    "message" in responseData &&
                    typeof responseData.message ===
                        "string"
                ) {
                    return responseData.message;
                }

                if (
                    "title" in responseData &&
                    typeof responseData.title ===
                        "string"
                ) {
                    return responseData.title;
                }
            }
        }

        if (error instanceof Error) {
            return error.message;
        }

        return fallbackMessage;
    };

    useEffect(() => {
        const loadAppliedJobs = async () => {
            setLoadingAppliedJobs(true);
            setErrorMessage("");

            try {
                const response =
                    await api.get<
                        AppliedJobResponse[]
                    >(
                        "/JobApplication/mine"
                    );

                setAppliedJobs(
                    Array.isArray(response.data)
                        ? response.data
                        : []
                );
            } catch (error) {
                setErrorMessage(
                    getErrorMessage(
                        error,
                        "Applied jobs could not be loaded."
                    )
                );
            } finally {
                setLoadingAppliedJobs(false);
            }
        };

        void loadAppliedJobs();
    }, []);

    const handleLogout = () => {
        localStorage.removeItem("token");
        localStorage.removeItem("userId");
        localStorage.removeItem("fullName");
        localStorage.removeItem("email");
        localStorage.removeItem("expiresAt");

        navigate("/auth", {
            replace: true,
        });
    };

    const handleOpenJob = (
        jobUrl: string
    ) => {
        if (!jobUrl?.trim()) {
            setErrorMessage(
                "This job does not have a valid application URL."
            );

            return;
        }

        window.open(
            jobUrl,
            "_blank",
            "noopener,noreferrer"
        );
    };

    const handleRemoveApplication = async (
        applicationId: number
    ) => {
        if (
            removingApplicationIds.has(
                applicationId
            )
        ) {
            return;
        }

       
        

      

        setRemovingApplicationIds(
            (currentIds) => {
                const updatedIds =
                    new Set(currentIds);

                updatedIds.add(applicationId);

                return updatedIds;
            }
        );

        try {
            await api.delete(
                `/JobApplication/${applicationId}`
            );

            setAppliedJobs((currentJobs) =>
                currentJobs.filter(
                    (job) =>
                        job.applicationId !==
                        applicationId
                )
            );

            setSuccessMessage(
                "Application removed successfully."
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The application could not be removed."
                )
            );
        } finally {
            setRemovingApplicationIds(
                (currentIds) => {
                    const updatedIds =
                        new Set(currentIds);

                    updatedIds.delete(
                        applicationId
                    );

                    return updatedIds;
                }
            );
        }
    };

    const formatAppliedDate = (
        dateValue: string
    ) => {
        const parsedDate =
            new Date(dateValue);

        if (
            Number.isNaN(
                parsedDate.getTime()
            )
        ) {
            return "Date unavailable";
        }

        return new Intl.DateTimeFormat(
            "en",
            {
                year: "numeric",
                month: "short",
                day: "numeric",
            }
        ).format(parsedDate);
    };

    return (
        <div className="dashboard-layout">
            <aside
                className={
                    sidebarOpen
                        ? "dashboard-sidebar dashboard-sidebar--open"
                        : "dashboard-sidebar"
                }
            >
                <div className="sidebar-brand">
                    <div className="sidebar-logo">
                        CM
                    </div>

                    <div>
                        <h2>CareerMatch</h2>
                        <span>Job Seeker</span>
                    </div>
                </div>

                <nav className="sidebar-navigation">
                    <NavLink
                        to="/dashboard"
                        end
                        className={({
                            isActive,
                        }) =>
                            isActive
                                ? "sidebar-link sidebar-link--active"
                                : "sidebar-link"
                        }
                        onClick={() =>
                            setSidebarOpen(false)
                        }
                    >
                        <span className="sidebar-link-icon">
                            ⌕
                        </span>

                        Find Jobs
                    </NavLink>

                    <NavLink
                        to="/saved-jobs"
                        className={({
                            isActive,
                        }) =>
                            isActive
                                ? "sidebar-link sidebar-link--active"
                                : "sidebar-link"
                        }
                        onClick={() =>
                            setSidebarOpen(false)
                        }
                    >
                        <span className="sidebar-link-icon">
                            ♡
                        </span>

                        Saved Jobs
                    </NavLink>

                    <NavLink
                        to="/applied-jobs"
                        className={({
                            isActive,
                        }) =>
                            isActive
                                ? "sidebar-link sidebar-link--active"
                                : "sidebar-link"
                        }
                        onClick={() =>
                            setSidebarOpen(false)
                        }
                    >
                        <span className="sidebar-link-icon">
                            ✓
                        </span>

                        Applied Jobs
                    </NavLink>
                </nav>

                <button
                    type="button"
                    className="sidebar-logout"
                    onClick={handleLogout}
                >
                    <span className="sidebar-link-icon">
                        ↪
                    </span>

                    Logout
                </button>
            </aside>

            {sidebarOpen && (
                <button
                    type="button"
                    className="dashboard-overlay"
                    aria-label="Close navigation"
                    onClick={() =>
                        setSidebarOpen(false)
                    }
                />
            )}

            <main className="dashboard-main">
                <header className="dashboard-header">
                    <div className="dashboard-header-left">
                        <button
                            type="button"
                            className="sidebar-toggle"
                            aria-label="Open navigation"
                            onClick={() =>
                                setSidebarOpen(true)
                            }
                        >
                            ☰
                        </button>

                        <div>
                            <p className="dashboard-eyebrow">
                                Career dashboard
                            </p>

                            <h1>
                                Applied Jobs
                            </h1>

                            <span>
                                Review the jobs you
                                opened for application.
                            </span>
                        </div>
                    </div>

                    <div className="dashboard-user">
                        <div className="dashboard-user-avatar">
                            {userInitials}
                        </div>

                        <div className="dashboard-user-details">
                            <strong>
                                {fullName}
                            </strong>

                            <span>
                                {email ||
                                    "Job Seeker"}
                            </span>
                        </div>
                    </div>
                </header>

                <section className="dashboard-content">
                    {errorMessage && (
                        <div
                            className="dashboard-card"
                            role="alert"
                            style={{
                                borderColor:
                                    "rgba(255, 105, 135, 0.45)",
                            }}
                        >
                            {errorMessage}
                        </div>
                    )}

                    {successMessage && (
                        <div
                            className="dashboard-card"
                            role="status"
                            style={{
                                borderColor:
                                    "rgba(130, 232, 181, 0.35)",
                            }}
                        >
                            {successMessage}
                        </div>
                    )}

                    {loadingAppliedJobs && (
                        <section className="dashboard-empty-state">
                            <p className="empty-state-label">
                                Loading
                            </p>

                            <h2>
                                Loading your applied
                                jobs...
                            </h2>
                        </section>
                    )}

                    {!loadingAppliedJobs &&
                        appliedJobs.length ===
                            0 && (
                            <section className="dashboard-empty-state">
                                <div className="empty-state-glow" />

                                <div className="empty-state-visual">
                                    <div className="empty-orbit empty-orbit--outer">
                                        <span className="orbit-dot orbit-dot--one" />
                                        <span className="orbit-dot orbit-dot--two" />
                                    </div>

                                    <div className="empty-orbit empty-orbit--inner">
                                        <span className="orbit-dot orbit-dot--three" />
                                    </div>

                                    <div className="empty-state-center">
                                        <span>
                                            ✓
                                        </span>
                                    </div>
                                </div>

                                <p className="empty-state-label">
                                    Applied jobs
                                </p>

                                <h2>
                                    No applied jobs yet
                                </h2>

                                <p className="empty-state-description">
                                    Jobs you open through
                                    CareerMatch will
                                    appear here.
                                </p>

                                <button
                                    type="button"
                                    className="dashboard-primary-button"
                                    style={{
                                        marginTop:
                                            "26px",
                                    }}
                                    onClick={() =>
                                        navigate(
                                            "/dashboard"
                                        )
                                    }
                                >
                                    Find Jobs
                                </button>
                            </section>
                        )}

                    {!loadingAppliedJobs &&
                        appliedJobs.length >
                            0 && (
                            <section className="applied-jobs-grid">
                                {appliedJobs.map(
                                    (job) => {
                                        const isRemoving =
                                            removingApplicationIds.has(
                                                job.applicationId
                                            );

                                        const scoreIsAvailable =
                                            job.matchScore !==
                                                null &&
                                            job.matchScore !==
                                                undefined;

                                        return (
                                            <article
                                                key={
                                                    job.applicationId
                                                }
                                                className="dashboard-card"
                                            >
                                                <div className="applied-job-card-content">
                                                    <div className="applied-job-card-header">
                                                        <div>
                                                            <p className="card-label">
                                                                {
                                                                    job.companyName
                                                                }
                                                            </p>

                                                            <h2>
                                                                {
                                                                    job.title
                                                                }
                                                            </h2>

                                                            <span className="applied-job-location">
                                                                {job.city
                                                                    ? `${job.city}, ${job.country}`
                                                                    : job.country}
                                                            </span>
                                                        </div>

                                                        <span className="feature-chip">
                                                            Applied{" "}
                                                            {formatAppliedDate(
                                                                job.appliedAt
                                                            )}
                                                        </span>
                                                    </div>

                                                    {scoreIsAvailable ? (
                                                        <div className="applied-job-score">
                                                            <div>
                                                                <p className="card-label">
                                                                    Match
                                                                    score
                                                                </p>

                                                                <strong>
                                                                    {
                                                                        job.matchScore
                                                                    }
                                                                    %
                                                                </strong>
                                                            </div>

                                                            {job.matchExplanation && (
                                                                <div>
                                                                    <p className="card-label">
                                                                        Explanation
                                                                    </p>

                                                                    <span>
                                                                        {
                                                                            job.matchExplanation
                                                                        }
                                                                    </span>
                                                                </div>
                                                            )}

                                                            {job.recommendation && (
                                                                <div>
                                                                    <p className="card-label">
                                                                        Recommendation
                                                                    </p>

                                                                    <span>
                                                                        {
                                                                            job.recommendation
                                                                        }
                                                                    </span>
                                                                </div>
                                                            )}
                                                        </div>
                                                    ) : (
                                                        <div className="applied-job-no-score">
                                                            No match
                                                            score was
                                                            available
                                                            when this
                                                            application
                                                            was
                                                            opened.
                                                        </div>
                                                    )}

                                                    <div className="applied-job-actions">
                                                        <button
                                                            type="button"
                                                            className="dashboard-primary-button"
                                                            onClick={() =>
                                                                handleOpenJob(
                                                                    job.jobUrl
                                                                )
                                                            }
                                                        >
                                                            Open
                                                            Job
                                                        </button>

                                                        <button
                                                            type="button"
                                                            className="dashboard-primary-button applied-job-remove-button"
                                                            disabled={
                                                                isRemoving
                                                            }
                                                            onClick={() =>
                                                                handleRemoveApplication(
                                                                    job.applicationId
                                                                )
                                                            }
                                                        >
                                                            {isRemoving
                                                                ? "Removing..."
                                                                : "Remove"}
                                                        </button>
                                                    </div>
                                                </div>
                                            </article>
                                        );
                                    }
                                )}
                            </section>
                        )}
                </section>
            </main>
        </div>
    );
}

export default AppliedJobsPage;