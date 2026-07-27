import { useEffect, useMemo, useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import axios from "axios";
import api from "../services/api";
import "./SavedJobsPage.css";

interface SavedJobResponse {
    savedJobId: number;
    jobId: number;
    title: string;
    companyName: string;
    country: string;
    city: string | null;
    jobUrl: string;
    matchScoreAtSave: number | null;
    savedMatchExplanation: string | null;
    savedAt: string;
}

interface SavedJobScoreResponse {
    jobId: number;
    matchScore: number;
    matchExplanation: string | null;
    recommendation: string | null;
}

interface ApplyResponse {
    success: boolean;
    message: string;
    jobUrl: string;
    applicationId: number;
    hasCV: boolean;
}

interface ApplyModalState {
    applicationId: number;
    jobUrl: string;
    jobTitle: string;
    companyName: string;
    hasCV: boolean;
}

type GeneratedDocumentType =
    | "cv"
    | "coverLetter"
    | "interviewQuestions";

function SavedJobsPage() {
    const navigate = useNavigate();

    const [sidebarOpen, setSidebarOpen] =
        useState(false);

    const [savedJobs, setSavedJobs] =
        useState<SavedJobResponse[]>([]);

    const [loadingSavedJobs, setLoadingSavedJobs] =
        useState(true);

    const [removingJobIds, setRemovingJobIds] =
        useState<Set<number>>(new Set());

    const [calculatingJobIds, setCalculatingJobIds] =
        useState<Set<number>>(new Set());

    const [refreshingJobIds, setRefreshingJobIds] =
        useState<Set<number>>(new Set());

    const [openingJobIds, setOpeningJobIds] =
        useState<Set<number>>(new Set());

    const [applyModal, setApplyModal] =
        useState<ApplyModalState | null>(null);

    const [generatingDocuments, setGeneratingDocuments] =
        useState<Record<GeneratedDocumentType, boolean>>({
            cv: false,
            coverLetter: false,
            interviewQuestions: false,
        });

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

    useEffect(() => {
        const loadSavedJobs = async () => {
            setLoadingSavedJobs(true);
            setErrorMessage("");

            try {
                const response =
                    await api.get<SavedJobResponse[]>(
                        "/SavedJob/mine"
                    );

                setSavedJobs(
                    Array.isArray(response.data)
                        ? response.data
                        : []
                );
            } catch (error) {
                setErrorMessage(
                    getErrorMessage(
                        error,
                        "Saved jobs could not be loaded."
                    )
                );
            } finally {
                setLoadingSavedJobs(false);
            }
        };

        void loadSavedJobs();
    }, []);

    const getErrorMessage = (
        error: unknown,
        fallbackMessage: string
    ) => {
        if (axios.isAxiosError(error)) {
            const responseData = error.response?.data;

            if (typeof responseData === "string") {
                return responseData;
            }

            if (
                responseData &&
                typeof responseData === "object"
            ) {
                if (
                    "message" in responseData &&
                    typeof responseData.message === "string"
                ) {
                    return responseData.message;
                }

                if (
                    "title" in responseData &&
                    typeof responseData.title === "string"
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

    const handleOpenJob = async (
        job: SavedJobResponse
    ) => {
        if (openingJobIds.has(job.jobId)) {
            return;
        }

        setSuccessMessage("");
        setErrorMessage("");

        setOpeningJobIds((currentIds) => {
            const updatedIds = new Set(currentIds);
            updatedIds.add(job.jobId);
            return updatedIds;
        });

        try {
            const response =
                await api.post<ApplyResponse>(
                    "/JobApplication/apply",
                    {
                        jobId: job.jobId,
                    }
                );

            const destinationUrl =
                response.data.jobUrl ||
                job.jobUrl;

            if (!destinationUrl?.trim()) {
                throw new Error(
                    "This saved job does not have a valid URL."
                );
            }

            setApplyModal({
                applicationId:
                    response.data.applicationId,
                jobUrl: destinationUrl,
                jobTitle: job.title,
                companyName: job.companyName,
                hasCV: response.data.hasCV,
            });

            setSuccessMessage(
                response.data.message ||
                    "Application recorded successfully."
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The application could not be recorded."
                )
            );
        } finally {
            setOpeningJobIds((currentIds) => {
                const updatedIds = new Set(currentIds);
                updatedIds.delete(job.jobId);
                return updatedIds;
            });
        }
    };

    const downloadGeneratedPdf = async (
        documentType: GeneratedDocumentType
    ) => {
        if (!applyModal || generatingDocuments[documentType]) {
            return;
        }

        if (
            !applyModal.hasCV &&
            (documentType === "cv" ||
                documentType === "coverLetter")
        ) {
            setErrorMessage(
                "Upload a CV to CareerMatch before using this option."
            );
            return;
        }

        setSuccessMessage("");
        setErrorMessage("");

        setGeneratingDocuments((current) => ({
            ...current,
            [documentType]: true,
        }));

        const endpointByType: Record<
            GeneratedDocumentType,
            string
        > = {
            cv: "/GeneratedCV/generate",
            coverLetter:
                "/GeneratedCoverLetter/generate",
            interviewQuestions:
                "/GeneratedInterviewQuestions/generate",
        };

        const fallbackNameByType: Record<
            GeneratedDocumentType,
            string
        > = {
            cv: "refined-cv.pdf",
            coverLetter: "cover-letter.pdf",
            interviewQuestions:
                "interview-questions.pdf",
        };

        try {
            const response = await api.post<Blob>(
                endpointByType[documentType],
                {
                    applicationId:
                        applyModal.applicationId,
                },
                {
                    responseType: "blob",
                }
            );

            const contentDisposition =
                response.headers["content-disposition"] as
                    | string
                    | undefined;

            const fileNameMatch =
                contentDisposition?.match(
                    /filename\*?=(?:UTF-8''|")?([^";]+)/i
                );

            const fileName = fileNameMatch?.[1]
                ? decodeURIComponent(
                      fileNameMatch[1].replace(/"/g, "")
                  )
                : fallbackNameByType[documentType];

            const downloadUrl =
                URL.createObjectURL(response.data);

            const link =
                document.createElement("a");

            link.href = downloadUrl;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(downloadUrl);

            const successByType: Record<
                GeneratedDocumentType,
                string
            > = {
                cv: "Your refined CV was downloaded.",
                coverLetter:
                    "Your cover letter was downloaded.",
                interviewQuestions:
                    "Your interview questions were downloaded.",
            };

            setSuccessMessage(
                successByType[documentType]
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The PDF could not be generated."
                )
            );
        } finally {
            setGeneratingDocuments((current) => ({
                ...current,
                [documentType]: false,
            }));
        }
    };

    const closeApplyModal = () => {
        const isGenerating = Object.values(
            generatingDocuments
        ).some(Boolean);

        if (!isGenerating) {
            setApplyModal(null);
        }
    };

    const continueToJobApplication = () => {
        if (!applyModal?.jobUrl) {
            setErrorMessage(
                "This job does not have an application URL."
            );
            return;
        }

        window.open(
            applyModal.jobUrl,
            "_blank",
            "noopener,noreferrer"
        );

        setApplyModal(null);
    };

    const handleCalculateScore = async (
        jobId: number
    ) => {
        if (calculatingJobIds.has(jobId)) {
            return;
        }

        setSuccessMessage("");
        setErrorMessage("");

        setCalculatingJobIds((currentIds) => {
            const updatedIds = new Set(currentIds);
            updatedIds.add(jobId);
            return updatedIds;
        });

        try {
            const response =
                await api.post<SavedJobScoreResponse>(
                    `/SavedJob/${jobId}/calculate-score`
                );

            setSavedJobs((currentJobs) =>
                currentJobs.map((job) =>
                    job.jobId === jobId
                        ? {
                              ...job,
                              matchScoreAtSave:
                                  response.data.matchScore,
                              savedMatchExplanation:
                                  response.data.matchExplanation,
                          }
                        : job
                )
            );

            setSuccessMessage(
                "Match score calculated successfully."
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The match score could not be calculated."
                )
            );
        } finally {
            setCalculatingJobIds((currentIds) => {
                const updatedIds = new Set(currentIds);
                updatedIds.delete(jobId);
                return updatedIds;
            });
        }
    };

    const handleRefreshScore = async (
        jobId: number
    ) => {
        if (
            calculatingJobIds.has(jobId) ||
            refreshingJobIds.has(jobId)
        ) {
            return;
        }

        setSuccessMessage("");
        setErrorMessage("");

        setRefreshingJobIds((currentIds) => {
            const updatedIds = new Set(currentIds);
            updatedIds.add(jobId);
            return updatedIds;
        });

        try {
            const response =
                await api.post<SavedJobScoreResponse>(
                    `/SavedJob/${jobId}/refresh-score`
                );

            setSavedJobs((currentJobs) =>
                currentJobs.map((job) =>
                    job.jobId === jobId
                        ? {
                              ...job,
                              matchScoreAtSave:
                                  response.data.matchScore,
                              savedMatchExplanation:
                                  response.data.matchExplanation,
                          }
                        : job
                )
            );

            setSuccessMessage(
                "Match score refreshed successfully."
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The match score could not be refreshed."
                )
            );
        } finally {
            setRefreshingJobIds((currentIds) => {
                const updatedIds = new Set(currentIds);
                updatedIds.delete(jobId);
                return updatedIds;
            });
        }
    };

    const handleRemoveSavedJob = async (
        jobId: number
    ) => {
        if (removingJobIds.has(jobId)) {
            return;
        }

        setSuccessMessage("");
        setErrorMessage("");

        setRemovingJobIds((currentIds) => {
            const updatedIds =
                new Set(currentIds);

            updatedIds.add(jobId);

            return updatedIds;
        });

        try {
            await api.delete(
                `/SavedJob/${jobId}`
            );

            setSavedJobs((currentJobs) =>
                currentJobs.filter(
                    (job) => job.jobId !== jobId
                )
            );

            setSuccessMessage(
                "Job removed from saved jobs."
            );
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    "The job could not be removed."
                )
            );
        } finally {
            setRemovingJobIds((currentIds) => {
                const updatedIds =
                    new Set(currentIds);

                updatedIds.delete(jobId);

                return updatedIds;
            });
        }
    };

    const formatSavedDate = (
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
                        className={({ isActive }) =>
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
                        className={({ isActive }) =>
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
                        className={({ isActive }) =>
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

                            <h1>Saved Jobs</h1>

                            <span>
                                Review and open the jobs
                                you saved.
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
                                {email || "Job Seeker"}
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

                    {loadingSavedJobs && (
                        <section className="dashboard-empty-state">
                            <p className="empty-state-label">
                                Loading
                            </p>

                            <h2>
                                Loading your saved jobs...
                            </h2>
                        </section>
                    )}

                    {!loadingSavedJobs &&
                        savedJobs.length === 0 && (
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
                                        <span>♡</span>
                                    </div>
                                </div>

                                <p className="empty-state-label">
                                    Saved jobs
                                </p>

                                <h2>
                                    No saved jobs yet
                                </h2>

                                <p className="empty-state-description">
                                    Save interesting jobs while
                                    searching and they will appear
                                    here.
                                </p>

                                <button
                                    type="button"
                                    className="dashboard-primary-button"
                                    style={{
                                        marginTop: "26px",
                                    }}
                                    onClick={() =>
                                        navigate("/dashboard")
                                    }
                                >
                                    Find Jobs
                                </button>
                            </section>
                        )}

                    {!loadingSavedJobs &&
                        savedJobs.length > 0 && (
                            <section
                                style={{
                                    display: "grid",
                                    gap: "18px",
                                }}
                            >
                                {savedJobs.map((job) => {
                                    const isRemoving =
                                        removingJobIds.has(
                                            job.jobId
                                        );

                                    const isCalculating =
                                        calculatingJobIds.has(
                                            job.jobId
                                        );

                                    const isRefreshing =
                                        refreshingJobIds.has(
                                            job.jobId
                                        );

                                    const isOpening =
                                        openingJobIds.has(
                                            job.jobId
                                        );

                                    const scoreIsAvailable =
                                        job.matchScoreAtSave !==
                                            null &&
                                        job.matchScoreAtSave !==
                                            undefined;

                                    return (
                                        <article
                                            key={job.savedJobId}
                                            className="dashboard-card"
                                        >
                                            <div
                                                style={{
                                                    position:
                                                        "relative",
                                                    zIndex: 1,
                                                    display:
                                                        "grid",
                                                    gap: "20px",
                                                }}
                                            >
                                                <div
                                                    style={{
                                                        display:
                                                            "flex",
                                                        justifyContent:
                                                            "space-between",
                                                        alignItems:
                                                            "flex-start",
                                                        gap: "20px",
                                                        flexWrap:
                                                            "wrap",
                                                    }}
                                                >
                                                    <div>
                                                        <p className="card-label">
                                                            {
                                                                job.companyName
                                                            }
                                                        </p>

                                                        <h2
                                                            style={{
                                                                margin:
                                                                    "0 0 9px",
                                                            }}
                                                        >
                                                            {
                                                                job.title
                                                            }
                                                        </h2>

                                                        <span
                                                            style={{
                                                                color:
                                                                    "var(--dashboard-muted)",
                                                                fontSize:
                                                                    "13px",
                                                            }}
                                                        >
                                                            {job.city
                                                                ? `${job.city}, ${job.country}`
                                                                : job.country}
                                                        </span>
                                                    </div>

                                                    <span className="feature-chip">
                                                        Saved{" "}
                                                        {formatSavedDate(
                                                            job.savedAt
                                                        )}
                                                    </span>
                                                </div>

                                                {scoreIsAvailable ? (
                                                    <div
                                                        style={{
                                                            display:
                                                                "grid",
                                                            gap: "12px",
                                                            padding:
                                                                "18px",
                                                            border:
                                                                "1px solid var(--dashboard-border)",
                                                            borderRadius:
                                                                "16px",
                                                            background:
                                                                "rgba(155, 108, 255, 0.055)",
                                                        }}
                                                    >
                                                        <div>
                                                            <p className="card-label">
                                                                Match score
                                                            </p>

                                                            <strong
                                                                style={{
                                                                    fontSize:
                                                                        "30px",
                                                                }}
                                                            >
                                                                {
                                                                    job.matchScoreAtSave
                                                                }
                                                                %
                                                            </strong>
                                                        </div>

                                                        {job.savedMatchExplanation && (
                                                            <div>
                                                                <p className="card-label">
                                                                    Explanation
                                                                </p>

                                                                <span>
                                                                    {
                                                                        job.savedMatchExplanation
                                                                    }
                                                                </span>
                                                            </div>
                                                        )}

                                                        <button
                                                            type="button"
                                                            className="dashboard-primary-button"
                                                            style={{
                                                                width:
                                                                    "fit-content",
                                                                marginTop:
                                                                    "8px",
                                                            }}
                                                            disabled={
                                                                isCalculating ||
                                                                isRefreshing
                                                            }
                                                            onClick={() =>
                                                                handleRefreshScore(
                                                                    job.jobId
                                                                )
                                                            }
                                                        >
                                                            {isRefreshing
                                                                ? "Refreshing..."
                                                                : "Refresh Score"}
                                                        </button>
                                                    </div>
                                                ) : (
                                                    <div
                                                        style={{
                                                            display:
                                                                "grid",
                                                            gap: "14px",
                                                            padding:
                                                                "18px",
                                                            border:
                                                                "1px solid var(--dashboard-border)",
                                                            borderRadius:
                                                                "16px",
                                                            color:
                                                                "var(--dashboard-muted)",
                                                            background:
                                                                "rgba(255, 255, 255, 0.025)",
                                                            fontSize:
                                                                "13px",
                                                            lineHeight:
                                                                1.6,
                                                        }}
                                                    >
                                                        <span>
                                                            No match score has
                                                            been calculated for
                                                            this saved job.
                                                        </span>

                                                        <div
                                                            style={{
                                                                display:
                                                                    "flex",
                                                                flexWrap:
                                                                    "wrap",
                                                                gap: "10px",
                                                            }}
                                                        >
                                                            <button
                                                                type="button"
                                                                className="dashboard-primary-button"
                                                                disabled={
                                                                    isCalculating ||
                                                                    isRefreshing
                                                                }
                                                                onClick={() =>
                                                                    handleCalculateScore(
                                                                        job.jobId
                                                                    )
                                                                }
                                                            >
                                                                {isCalculating
                                                                    ? "Calculating..."
                                                                    : "Calculate Score"}
                                                            </button>

                                                            <button
                                                                type="button"
                                                                className="dashboard-primary-button"
                                                                disabled={
                                                                    isCalculating ||
                                                                    isRefreshing
                                                                }
                                                                onClick={() =>
                                                                    handleRefreshScore(
                                                                        job.jobId
                                                                    )
                                                                }
                                                            >
                                                                {isRefreshing
                                                                    ? "Refreshing..."
                                                                    : "Refresh Score"}
                                                            </button>
                                                        </div>
                                                    </div>
                                                )}

                                                <div
                                                    style={{
                                                        display:
                                                            "flex",
                                                        flexWrap:
                                                            "wrap",
                                                        gap: "10px",
                                                    }}
                                                >
                                                    <button
                                                        type="button"
                                                        className="dashboard-primary-button"
                                                        disabled={
                                                            isOpening
                                                        }
                                                        onClick={() =>
                                                            handleOpenJob(
                                                                job
                                                            )
                                                        }
                                                    >
                                                        {isOpening
                                                            ? "Opening..."
                                                            : "Open Job"}
                                                    </button>

                                                    <button
                                                        type="button"
                                                        className="dashboard-primary-button"
                                                        disabled={
                                                            isRemoving
                                                        }
                                                        onClick={() =>
                                                            handleRemoveSavedJob(
                                                                job.jobId
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
                                })}
                            </section>
                        )}
                </section>
            </main>

            {applyModal && (
                <div
                    className="apply-modal-backdrop"
                    role="presentation"
                    onMouseDown={(event) => {
                        if (event.target === event.currentTarget) {
                            closeApplyModal();
                        }
                    }}
                >
                    <section
                        className="apply-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="apply-modal-title"
                    >
                        <button
                            type="button"
                            className="apply-modal-close"
                            aria-label="Close application options"
                            onClick={closeApplyModal}
                            disabled={Object.values(
                                generatingDocuments
                            ).some(Boolean)}
                        >
                            ×
                        </button>

                        <div className="apply-modal-heading">
                            <div className="apply-modal-icon">
                                ✦
                            </div>

                            <div>
                                <p className="card-label">
                                    Application toolkit
                                </p>

                                <h2 id="apply-modal-title">
                                    Prepare before you continue
                                </h2>

                                <p>
                                    {applyModal.jobTitle} at{" "}
                                    {applyModal.companyName}
                                </p>
                            </div>
                        </div>

                        <div className="apply-modal-options">
                            <button
                                type="button"
                                className="apply-option-card"
                                onClick={() =>
                                    downloadGeneratedPdf("cv")
                                }
                                disabled={
                                    generatingDocuments.cv ||
                                    !applyModal.hasCV
                                }
                            >
                                <span className="apply-option-icon">
                                    CV
                                </span>

                                <span className="apply-option-copy">
                                    <strong>Refine CV</strong>
                                    <small>
                                        {applyModal.hasCV
                                            ? "Tailor your latest CV to this role and download it immediately."
                                            : "Upload a CV to CareerMatch to unlock CV refinement."}
                                    </small>
                                </span>

                                <span className="apply-option-action">
                                    {!applyModal.hasCV
                                        ? "CV required"
                                        : generatingDocuments.cv
                                          ? "Generating..."
                                          : "Generate"}
                                </span>
                            </button>

                            <button
                                type="button"
                                className="apply-option-card"
                                onClick={() =>
                                    downloadGeneratedPdf(
                                        "coverLetter"
                                    )
                                }
                                disabled={
                                    generatingDocuments.coverLetter ||
                                    !applyModal.hasCV
                                }
                            >
                                <span className="apply-option-icon">
                                    ✉
                                </span>

                                <span className="apply-option-copy">
                                    <strong>
                                        Generate Cover Letter
                                    </strong>
                                    <small>
                                        {applyModal.hasCV
                                            ? "Create a focused letter for this company and position."
                                            : "Upload a CV to CareerMatch to generate a personalized cover letter."}
                                    </small>
                                </span>

                                <span className="apply-option-action">
                                    {!applyModal.hasCV
                                        ? "CV required"
                                        : generatingDocuments.coverLetter
                                          ? "Generating..."
                                          : "Generate"}
                                </span>
                            </button>

                            <button
                                type="button"
                                className="apply-option-card"
                                onClick={() =>
                                    downloadGeneratedPdf(
                                        "interviewQuestions"
                                    )
                                }
                                disabled={
                                    generatingDocuments.interviewQuestions
                                }
                            >
                                <span className="apply-option-icon">
                                    ?
                                </span>

                                <span className="apply-option-copy">
                                    <strong>
                                        Generate Interview Questions
                                    </strong>
                                    <small>
                                        Download tailored practical and
                                        theoretical interview preparation.
                                    </small>
                                </span>

                                <span className="apply-option-action">
                                    {generatingDocuments.interviewQuestions
                                        ? "Generating..."
                                        : "Generate"}
                                </span>
                            </button>
                        </div>

                        <div className="apply-modal-footer">
                            <p>
                                {applyModal.hasCV
                                    ? "Generating documents is optional. You can continue whenever you are ready."
                                    : "You can still generate interview questions and continue to the external application page without a CareerMatch CV."}
                            </p>

                            <button
                                type="button"
                                className="dashboard-primary-button apply-continue-button"
                                onClick={continueToJobApplication}
                            >
                                Continue to Job Application
                                <span aria-hidden="true">↗</span>
                            </button>
                        </div>
                    </section>
                </div>
            )}
        </div>
    );
}

export default SavedJobsPage;