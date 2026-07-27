import { useEffect, useMemo, useState } from "react";
import CreatableSelect from "react-select/creatable";
import type {
    ChangeEvent,
    FormEvent,
    Dispatch,
    SetStateAction,
} from "react";
import {
    NavLink,
    useNavigate,
    useLocation,
} from "react-router-dom";
import axios from "axios";
import api from "../services/api";
import "./DashboardPage.css";
const loadingMessages = [
    "Searching...",
    "🌍 Collecting the latest openings...",
    "📋 Filtering jobs based on your preferences...",
    "📌 Organizing the best available positions...",
    "🚀 Preparing your personalized job list...",
    "⏳ Just a few more seconds..."
];

const cvUploadMessages = [
    "Uploading your CV...",
    "Analyzing your experience...",
    "Extracting your skills...",
    "Identifying your primary role...",
    "Organizing your career profile...",
    "Just a second more..."
];
const citiesByCountry: Record<string, string[]> = {
    lebanon: [
        "Beirut",
        "Tripoli",
        "Sidon",
        "Zahle"
    ],

    "saudi arabia": [
        "Riyadh",
        "Jeddah",
        "Dammam",
        "Mecca"
    ],

    "united arab emirates": [
        "Dubai",
        "Abu Dhabi",
        "Sharjah",
        "Ajman"
    ],

    qatar: [
        "Doha",
        "Al Rayyan",
        "Al Wakrah",
        "Lusail"
    ],

    kuwait: [
        "Kuwait City",
        "Hawalli",
        "Salmiya",
        "Al Ahmadi"
    ],

    oman: [
        "Muscat",
        "Salalah",
        "Sohar",
        "Nizwa"
    ],

    bahrain: [
        "Manama",
        "Riffa",
        "Muharraq",
        "Hamad Town"
    ],

    jordan: [
        "Amman",
        "Irbid",
        "Zarqa",
        "Aqaba"
    ],

    iraq: [
        "Baghdad",
        "Basra",
        "Erbil",
        "Mosul"
    ],

    egypt: [
        "Cairo",
        "Alexandria",
        "Giza",
        "Mansoura"
    ],

    morocco: [
        "Casablanca",
        "Rabat",
        "Marrakesh",
        "Tangier"
    ],

    tunisia: [
        "Tunis",
        "Sfax",
        "Sousse",
        "Bizerte"
    ],

    "united states": [
        "New York",
        "Los Angeles",
        "Chicago",
        "Houston"
    ],

    canada: [
        "Toronto",
        "Vancouver",
        "Montreal",
        "Calgary"
    ],

    mexico: [
        "Mexico City",
        "Guadalajara",
        "Monterrey",
        "Puebla"
    ],

    brazil: [
        "São Paulo",
        "Rio de Janeiro",
        "Brasília",
        "Belo Horizonte"
    ],

    "united kingdom": [
        "London",
        "Manchester",
        "Birmingham",
        "Liverpool"
    ],

    france: [
        "Paris",
        "Marseille",
        "Lyon",
        "Toulouse"
    ],

    italy: [
        "Rome",
        "Milan",
        "Naples",
        "Turin"
    ],

    spain: [
        "Madrid",
        "Barcelona",
        "Valencia",
        "Seville"
    ],

    india: [
        "Mumbai",
        "Delhi",
        "Bengaluru",
        "Hyderabad"
    ],

    japan: [
        "Tokyo",
        "Osaka",
        "Yokohama",
        "Kyoto"
    ]
};
interface JobSearchForm {
    country: string;
    city: string;
    role: string;
    workType: string;
    employmentType: string;
}

interface JobSearchResponse {
    jobId: number;
    externalJobId: string;
    title: string;
    companyName: string;
    country: string;
    city: string | null;
    description: string;
    jobUrl: string;
    employmentType: string | null;
    workMode: string | null;
    postedDate: string | null;
    matchScore: number | null;
    matchExplanation: string | null;
    recommendation: string | null;
    matchStatus: string | null;
}

interface MatchResultResponse {
    jobId: number;
    matchScore: number;
    matchExplanation: string | null;
    recommendation: string | null;
    matchedSkills?: string[];
    missingSkills?: string[];
}

interface CVResponse {
    cvId: number;
    userId: number;
    originalFileName: string;
    storedFileName: string;
    filePath: string;
    uploadedAt: string;
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

interface SavedJobResponse {
    savedJobId: number;
    jobId: number;
}

interface AppliedJobResponse {
    applicationId: number;
    jobId: number;
}

const initialSearchForm: JobSearchForm = {
    country: "",
    city: "",
    role: "",
    workType: "",
    employmentType: "",
};

const SEARCH_JOBS_STORAGE_KEY =
    "careerMatchLatestSearchJobs";

const SEARCH_FORM_STORAGE_KEY =
    "careerMatchLatestSearchForm";

const HAS_SEARCHED_STORAGE_KEY =
    "careerMatchHasSearched";

const REVEALED_MATCHES_STORAGE_KEY =
    "careerMatchRevealedMatchJobIds";

const readStoredSearchForm = (): JobSearchForm => {
    try {
        const storedValue = sessionStorage.getItem(
            SEARCH_FORM_STORAGE_KEY
        );

        if (!storedValue) {
            return initialSearchForm;
        }

        return {
            ...initialSearchForm,
            ...(JSON.parse(
                storedValue
            ) as Partial<JobSearchForm>),
        };
    } catch {
        sessionStorage.removeItem(
            SEARCH_FORM_STORAGE_KEY
        );

        return initialSearchForm;
    }
};

const readStoredJobs =
    (): JobSearchResponse[] => {
        try {
            const storedValue =
                sessionStorage.getItem(
                    SEARCH_JOBS_STORAGE_KEY
                );

            if (!storedValue) {
                return [];
            }

            const parsed = JSON.parse(storedValue);

            return Array.isArray(parsed)
                ? parsed
                : [];
        } catch {
            sessionStorage.removeItem(
                SEARCH_JOBS_STORAGE_KEY
            );

            return [];
        }
    };

const readStoredHasSearched = () =>
    sessionStorage.getItem(
        HAS_SEARCHED_STORAGE_KEY
    ) === "true";

const readStoredRevealedMatchJobIds = () => {
    try {
        const storedValue =
            sessionStorage.getItem(
                REVEALED_MATCHES_STORAGE_KEY
            );

        if (!storedValue) {
            return new Set<number>();
        }

        const parsed = JSON.parse(storedValue);

        if (!Array.isArray(parsed)) {
            return new Set<number>();
        }

        return new Set<number>(
            parsed.filter(
                (value): value is number =>
                    typeof value === "number"
            )
        );
    } catch {
        sessionStorage.removeItem(
            REVEALED_MATCHES_STORAGE_KEY
        );

        return new Set<number>();
    }
};

function DashboardPage() {
    const navigate = useNavigate();
    const location = useLocation();
    const [loadingMessageIndex, setLoadingMessageIndex] = useState(0);
    const [cvUploadMessageIndex, setCvUploadMessageIndex] =
        useState(0);

    const [sidebarOpen, setSidebarOpen] =
        useState(false);

    const [selectedFile, setSelectedFile] =
        useState<File | null>(null);

    const [uploadingCV, setUploadingCV] =
        useState(false);

    const [uploadedCV, setUploadedCV] =
        useState<CVResponse | null>(null);

    const [searchForm, setSearchForm] =
        useState<JobSearchForm>(
            readStoredSearchForm
        );

    const [jobs, setJobs] =
        useState<JobSearchResponse[]>(
            readStoredJobs
        );

    const [hasSearched, setHasSearched] =
        useState(
            readStoredHasSearched
        );

    const [searchingJobs, setSearchingJobs] =
        useState(false);

    const [calculatingJobIds, setCalculatingJobIds] =
        useState<Set<number>>(
            new Set()
        );

    /*
        A score is displayed only after the user presses
        Show Score during the current frontend session.

        Even if the backend returns a cached score during
        the initial search, it stays hidden until the user
        explicitly asks to see it.
    */
    const [revealedMatchJobIds, setRevealedMatchJobIds] =
        useState<Set<number>>(
            readStoredRevealedMatchJobIds
        );

    const [matchErrorsByJobId, setMatchErrorsByJobId] =
        useState<Record<number, string>>({});

    const [savingJobIds, setSavingJobIds] =
        useState<Set<number>>(
            new Set()
        );

    const [savedJobIds, setSavedJobIds] =
        useState<Set<number>>(
            new Set()
        );

    const [applyingJobIds, setApplyingJobIds] =
        useState<Set<number>>(
            new Set()
        );

    const [appliedJobIds, setAppliedJobIds] =
        useState<Set<number>>(
            new Set()
        );

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
useEffect(() => {
    if (!searchingJobs) {
        setLoadingMessageIndex(0);
        return;
    }

    const interval = setInterval(() => {
        setLoadingMessageIndex((previous) =>
            previous < loadingMessages.length - 1
                ? previous + 1
                : previous
        );
    }, 5000);

    return () => clearInterval(interval);
}, [searchingJobs]);

useEffect(() => {
    if (!uploadingCV) {
        setCvUploadMessageIndex(0);
        return;
    }

    const interval = window.setInterval(() => {
        setCvUploadMessageIndex((previousIndex) =>
            previousIndex < cvUploadMessages.length - 1
                ? previousIndex + 1
                : previousIndex
        );
    }, 3500);

    return () => {
        window.clearInterval(interval);
    };
}, [uploadingCV]);

    useEffect(() => {
        sessionStorage.setItem(
            SEARCH_FORM_STORAGE_KEY,
            JSON.stringify(searchForm)
        );
    }, [searchForm]);

    useEffect(() => {
        if (jobs.length > 0) {
            sessionStorage.setItem(
                SEARCH_JOBS_STORAGE_KEY,
                JSON.stringify(jobs)
            );
        } else {
            sessionStorage.removeItem(
                SEARCH_JOBS_STORAGE_KEY
            );
        }
    }, [jobs]);

    useEffect(() => {
        sessionStorage.setItem(
            HAS_SEARCHED_STORAGE_KEY,
            String(hasSearched)
        );
    }, [hasSearched]);

    useEffect(() => {
        sessionStorage.setItem(
            REVEALED_MATCHES_STORAGE_KEY,
            JSON.stringify(
                Array.from(
                    revealedMatchJobIds
                )
            )
        );
    }, [revealedMatchJobIds]);

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

    const clearMessages = () => {
        setSuccessMessage("");
        setErrorMessage("");
    };

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

    const updateIdSet = (
        setter: Dispatch<
SetStateAction<Set<number>>
>,
        jobId: number,
        shouldAdd: boolean
    ) => {
        setter((currentIds) => {
            const updatedIds =
                new Set(currentIds);

            if (shouldAdd) {
                updatedIds.add(jobId);
            } else {
                updatedIds.delete(jobId);
            }

            return updatedIds;
        });
    };

    const loadSavedJobs = async () => {
        try {
            const response =
                await api.get<SavedJobResponse[]>(
                    "/SavedJob/mine"
                );

            setSavedJobIds(
                new Set(
                    response.data.map(
                        job => job.jobId
                    )
                )
            );
        } catch {
            setSavedJobIds(new Set());
        }
    };

    const loadAppliedJobs = async () => {
        try {
            const response =
                await api.get<AppliedJobResponse[]>(
                    "/JobApplication/mine"
                );

            setAppliedJobIds(
                new Set(
                    response.data.map(
                        application =>
                            application.jobId
                    )
                )
            );
        } catch {
            setAppliedJobIds(new Set());
        }
    };

    useEffect(() => {
        void loadSavedJobs();
        void loadAppliedJobs();
    }, [location.pathname]);

    const normalizeJobsResponse = (
        responseData: unknown
    ): JobSearchResponse[] => {
        if (Array.isArray(responseData)) {
            return responseData as JobSearchResponse[];
        }

        if (
            responseData &&
            typeof responseData === "object"
        ) {
            const dataObject =
                responseData as Record<string, unknown>;

            if (Array.isArray(dataObject.jobs)) {
                return dataObject.jobs as JobSearchResponse[];
            }

            if (Array.isArray(dataObject.matches)) {
                return dataObject.matches as JobSearchResponse[];
            }

            if (
                typeof dataObject.jobId === "number"
            ) {
                return [
                    dataObject as unknown as JobSearchResponse,
                ];
            }
        }

        return [];
    };

    const normalizeMatchResponse = (
        responseData: unknown
    ): MatchResultResponse[] => {
        if (Array.isArray(responseData)) {
            return responseData as MatchResultResponse[];
        }

        if (
            !responseData ||
            typeof responseData !== "object"
        ) {
            return [];
        }

        const dataObject =
            responseData as Record<string, unknown>;

        if (Array.isArray(dataObject.matches)) {
            return dataObject.matches as MatchResultResponse[];
        }

        if (typeof dataObject.jobId === "number") {
            return [
                dataObject as unknown as MatchResultResponse,
            ];
        }

        /*
            ASP.NET serializes Dictionary<int, AIMatchResult>
            as an object whose keys are job IDs.
        */
        return Object.values(dataObject).filter(
            (value): value is MatchResultResponse =>
                Boolean(
                    value &&
                        typeof value === "object" &&
                        "jobId" in value &&
                        typeof (
                            value as { jobId?: unknown }
                        ).jobId === "number"
                )
        );
    };

    const handleLogout = () => {
        localStorage.removeItem("token");
        localStorage.removeItem("userId");
        localStorage.removeItem("fullName");
        localStorage.removeItem("email");
        localStorage.removeItem("expiresAt");

        sessionStorage.removeItem(
            SEARCH_JOBS_STORAGE_KEY
        );

        sessionStorage.removeItem(
            SEARCH_FORM_STORAGE_KEY
        );

        sessionStorage.removeItem(
            HAS_SEARCHED_STORAGE_KEY
        );

        sessionStorage.removeItem(
            REVEALED_MATCHES_STORAGE_KEY
        );

        navigate("/auth", {
            replace: true,
        });
    };

    const handleFileChange = (
        event: ChangeEvent<HTMLInputElement>
    ) => {
        clearMessages();

        const file =
            event.target.files?.[0] || null;

        if (!file) {
            setSelectedFile(null);
            return;
        }

        const isPdf =
            file.type === "application/pdf" ||
            file.name
                .toLowerCase()
                .endsWith(".pdf");

        if (!isPdf) {
            setSelectedFile(null);

            setErrorMessage(
                "Please select a PDF file."
            );

            event.target.value = "";
            return;
        }

        setSelectedFile(file);
    };

    const handleUploadCV = async () => {
        if (!selectedFile || uploadingCV) {
            return;
        }

        clearMessages();
        setUploadingCV(true);

        try {
            const formData =
                new FormData();

            /*
                The property name must be File because
                CVUploadRequest contains IFormFile File.
            */
            formData.append(
                "File",
                selectedFile
            );

            const response =
                await api.post<CVResponse>(
                    "/CV/upload",
                    formData
                );

            setUploadedCV(response.data);

            setSuccessMessage(
                `"${response.data.originalFileName}" was uploaded successfully.`
            );

            setSelectedFile(null);

            const fileInput =
                document.getElementById(
                    "cv-file-input"
                ) as HTMLInputElement | null;

            if (fileInput) {
                fileInput.value = "";
            }
        } catch (error) {
            /*
                The backend now returns meaningful validation
                messages (for example "Please upload a valid CV.").
                Display them directly to the user.
            */
            setUploadedCV(null);

            setErrorMessage(
                getErrorMessage(
                    error,
                    "CV upload failed. Please try again."
                )
            );
        } finally {
            setUploadingCV(false);
        }
    };

    const handleSearchInputChange = (
        event:
            | ChangeEvent<HTMLInputElement>
            | ChangeEvent<HTMLSelectElement>
    ) => {
        const {
            name,
            value,
        } = event.target;

        setSearchForm((currentForm) => ({
            ...currentForm,
            [name]: value,
        }));
    };

    const handleSearchJobs = async (
        event: FormEvent<HTMLFormElement>
    ) => {
        event.preventDefault();

        if (searchingJobs) {
            return;
        }

        clearMessages();

        if (
            !searchForm.country.trim() ||
            !searchForm.role.trim() ||
            !searchForm.workType ||
            !searchForm.employmentType
        ) {
            setErrorMessage(
                "Country, role, work mode, and employment type are required."
            );

            return;
        }

        setSearchingJobs(true);
        setHasSearched(true);

        setJobs([]);

        sessionStorage.removeItem(
            SEARCH_JOBS_STORAGE_KEY
        );

        /*
            A new search resets which cards were
            explicitly revealed by the user.
        */
        setRevealedMatchJobIds(
            new Set()
        );

        setSavedJobIds(
            new Set()
        );

        setMatchErrorsByJobId({});

        try {
            const requestBody = {
                country:
                    searchForm.country.trim(),

                city:
                    searchForm.city.trim(),

                role:
                    searchForm.role.trim(),

                workType:
                    searchForm.workType,

                employmentType:
                    searchForm.employmentType,
            };

            const response =
                await api.post<JobSearchResponse[]>(
                    "/JobSearch/search",
                    requestBody
                );

            const returnedJobs =
                normalizeJobsResponse(
                    response.data
                );

            setJobs(returnedJobs);

            sessionStorage.setItem(
                SEARCH_JOBS_STORAGE_KEY,
                JSON.stringify(returnedJobs)
            );

            await loadSavedJobs();

            if (returnedJobs.length === 0) {
                setSuccessMessage(
                    "The search completed, but no jobs matched the selected filters."
                );
            }
        } catch (error) {
            setJobs([]);

            setErrorMessage(
                getErrorMessage(
                    error,
                    "Job search failed. Please try again."
                )
            );
        } finally {
            setSearchingJobs(false);
        }
    };

    const handleCalculateMatch = async (
        jobId: number
    ) => {
        if (calculatingJobIds.has(jobId)) {
            return;
        }

        clearMessages();

        setMatchErrorsByJobId((currentErrors) => {
            const updatedErrors = { ...currentErrors };
            delete updatedErrors[jobId];
            return updatedErrors;
        });

        updateIdSet(
            setCalculatingJobIds,
            jobId,
            true
        );

        try {
            const requestBody = {
                jobIds: [jobId],
                country: searchForm.country.trim(),
                city: searchForm.city.trim(),
                role: searchForm.role.trim(),
                workType: searchForm.workType,
                employmentType:
                    searchForm.employmentType,
            };

            const response = await api.post(
                "/JobSearch/calculate-matches",
                requestBody
            );

            const calculatedMatches =
                normalizeMatchResponse(response.data);

            const calculatedMatch =
                calculatedMatches.find(
                    (match) => match.jobId === jobId
                );

            if (!calculatedMatch) {
                throw new Error(
                    "The match response did not contain the selected job."
                );
            }

            setJobs((currentJobs) =>
                currentJobs.map((job) =>
                    job.jobId === jobId
                        ? {
                              ...job,
                              matchScore:
                                  calculatedMatch.matchScore,
                              matchExplanation:
                                  calculatedMatch.matchExplanation,
                              recommendation:
                                  calculatedMatch.recommendation,
                              matchStatus: "calculated",
                          }
                        : job
                )
            );

            updateIdSet(
                setRevealedMatchJobIds,
                jobId,
                true
            );
        } catch (error) {
            const message = getErrorMessage(
                error,
                "The match score could not be calculated."
            );

            /*
                Keep the score hidden when matching fails.
                This includes the backend's no-CV 400 response.
            */
            updateIdSet(
                setRevealedMatchJobIds,
                jobId,
                false
            );

            setMatchErrorsByJobId((currentErrors) => ({
                ...currentErrors,
                [jobId]: message,
            }));
        } finally {
            updateIdSet(
                setCalculatingJobIds,
                jobId,
                false
            );
        }
    };

    const handleToggleSavedJob = async (
        jobId: number
    ) => {
        if (savingJobIds.has(jobId)) {
            return;
        }

        clearMessages();

        const isCurrentlySaved =
            savedJobIds.has(jobId);

        updateIdSet(
            setSavingJobIds,
            jobId,
            true
        );

        try {
            if (isCurrentlySaved) {
                await api.delete(
                    `/SavedJob/${jobId}`
                );

                await loadSavedJobs();

                setSuccessMessage(
                    "Job removed from saved jobs."
                );
            } else {
                await api.post(
                    "/SavedJob/save",
                    {
                        jobId,
                    }
                );

                await loadSavedJobs();

                setSuccessMessage(
                    "Job saved successfully."
                );
            }
        } catch (error) {
            setErrorMessage(
                getErrorMessage(
                    error,
                    isCurrentlySaved
                        ? "The job could not be removed from saved jobs."
                        : "The job could not be saved."
                )
            );
        } finally {
            updateIdSet(
                setSavingJobIds,
                jobId,
                false
            );
        }
    };

    const handleApplyForJob = async (
        job: JobSearchResponse
    ) => {
        if (
            applyingJobIds.has(job.jobId)
        ) {
            return;
        }

        clearMessages();

        updateIdSet(
            setApplyingJobIds,
            job.jobId,
            true
        );

        try {
            const response =
                await api.post<ApplyResponse>(
                    "/JobApplication/apply",
                    {
                        jobId: job.jobId,
                    }
                );

            await loadAppliedJobs();

            const destinationUrl =
                response.data.jobUrl ||
                job.jobUrl;

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
            updateIdSet(
                setApplyingJobIds,
                job.jobId,
                false
            );
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

        clearMessages();

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

    const formatPostedDate = (
        dateValue: string | null
    ) => {
        if (!dateValue) {
            return "Date unavailable";
        }

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
                                Find your next opportunity
                            </h1>

                            <span>
                                Search, compare, and apply
                                to relevant jobs.
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

                    <section className="dashboard-card cv-upload-card">
                        <div className="card-heading">
                            <div className="card-icon">
                                ↑
                            </div>

                            <div>
                                <p className="card-label">
                                    Candidate profile
                                </p>

                                <h2>
                                    Upload your CV
                                </h2>

                                <span>
                                   NOTE: Your latest uploaded CV is automatically used for matching. Upload a new one only if you've made changes.
                                </span>
                            </div>
                        </div>

                        <div className="cv-upload-area">
                            <input
                                id="cv-file-input"
                                className="cv-file-input"
                                type="file"
                                accept=".pdf,application/pdf"
                                onChange={
                                    handleFileChange
                                }
                            />

                            <label
                                className="cv-file-label"
                                htmlFor="cv-file-input"
                            >
                                <span className="upload-symbol">
                                    ↥
                                </span>

                                <div>
                                    <strong>
                                        {selectedFile
                                            ? selectedFile.name
                                            : uploadedCV
                                              ? uploadedCV.originalFileName
                                              : "Choose your CV"}
                                    </strong>

                                    <span>
                                        PDF files only
                                    </span>
                                </div>
                            </label>

                            <div className="cv-upload-action">
                                <button
                                    type="button"
                                    className="dashboard-primary-button"
                                    disabled={!selectedFile || uploadingCV}
                                    onClick={handleUploadCV}
                                >
                                    {uploadingCV
                                        ? "Processing CV..."
                                        : "Upload CV"}
                                </button>

                                {uploadingCV && (
                                    <div
                                        className="cv-upload-progress"
                                        role="status"
                                        aria-live="polite"
                                    >
                                        <span className="cv-upload-spinner" />

                                        <span className="cv-upload-message">
                                            {
                                                cvUploadMessages[
                                                    cvUploadMessageIndex
                                                ]
                                            }
                                        </span>
                                    </div>
                                )}
                            </div>
                        </div>
                    </section>

                    <section className="dashboard-card">
                        <div className="card-heading">
                            <div className="card-icon">
                                ⌕
                            </div>

                            <div>
                                <p className="card-label">
                                    Job discovery
                                </p>

                                <h2>
                                    Search for jobs
                                </h2>

                                <span>
                                    Choose your preferred
                                    role, location, work
                                    mode, and employment
                                    type.
                                </span>
                            </div>
                        </div>

                        <form
                            className="job-search-form"
                            onSubmit={
                                handleSearchJobs
                            }
                        >
                            <div className="form-field">
                                <label htmlFor="country">
                                    Country <span>*</span>
                                </label>

                              <select
    id="country"
    name="country"
    value={searchForm.country}
    onChange={handleSearchInputChange}
    disabled={searchingJobs}
    required
>
    <option value="" disabled>
        Select a country
    </option>


<option value="lebanon">
    Lebanon
</option>

<option value="saudi arabia">
    Saudi Arabia
</option>

<option value="united arab emirates">
    United Arab Emirates
</option>

<option value="qatar">
    Qatar
</option>

<option value="kuwait">
    Kuwait
</option>

<option value="oman">
    Oman
</option>

<option value="bahrain">
    Bahrain
</option>

<option value="jordan">
    Jordan
</option>

<option value="iraq">
    Iraq
</option>

<option value="egypt">
    Egypt
</option>

<option value="morocco">
    Morocco 
</option>

<option value="tunisia">
    Tunisia 
</option>

<option value="united states">
    United States 
</option>

<option value="canada">
    Canada 
</option>

<option value="mexico">
    Mexico 
</option>

<option value="brazil">
    Brazil
</option>

<option value="united kingdom">
    United Kingdom 
</option>

<option value="france">
    France 
</option>

<option value="italy">
    Italy 
</option>

<option value="spain">
    Spain 
</option>

<option value="india">
    India 
</option>

<option value="japan">
    Japan 
</option>
</select>
                            </div>

                            <div className="form-field">
                                <label htmlFor="city">
                                    City
                                </label>

                               <CreatableSelect
    inputId="city"
    name="city"
    options={
        searchForm.country
            ? (
                  citiesByCountry[
                      searchForm.country
                          .trim()
                          .toLowerCase()
                  ] ?? []
              ).map((city) => ({
                  value: city,
                  label: city
              }))
            : Object.entries(
                  citiesByCountry
              ).map(([country, cities]) => ({
                  label: country
                      .split(" ")
                      .map(
                          (word) =>
                              word
                                  .charAt(0)
                                  .toUpperCase() +
                              word.slice(1)
                      )
                      .join(" "),
                  options: cities.map((city) => ({
                      value: city,
                      label: city
                  }))
              }))
    }
    value={
        searchForm.city
            ? {
                  value: searchForm.city,
                  label: searchForm.city
              }
            : null
    }
    onChange={(selectedOption) => {
        setSearchForm((previous) => ({
            ...previous,
            city: selectedOption?.value ?? ""
        }));
    }}
    onCreateOption={(typedCity) => {
        const cleanCity = typedCity.trim();

        if (!cleanCity) {
            return;
        }

        setSearchForm((previous) => ({
            ...previous,
            city: cleanCity
        }));
    }}
    placeholder={
        searchForm.country
            ? "Select or type a city"
            : "Select country or type a city"
    }
    formatCreateLabel={(typedCity) =>
        `Use "${typedCity}"`
    }
    noOptionsMessage={({ inputValue }) =>
        inputValue
            ? `Press Enter to use "${inputValue}"`
            : "No suggested cities. You can type one."
    }
    isSearchable
    isClearable
    openMenuOnClick
    openMenuOnFocus
    closeMenuOnSelect
    isDisabled={searchingJobs}
    menuPosition="fixed"
    styles={{
        control: (base, state) => ({
            ...base,
            minHeight: "48px",
            borderRadius: "12px",
            backgroundColor: "#000",
            borderColor: state.isFocused
                ? "#555"
                : "#333",
            boxShadow: state.isFocused
                ? "0 0 0 2px rgba(255,255,255,0.15)"
                : "none",

            "&:hover": {
                borderColor: "#555"
            }
        }),

        input: (base) => ({
            ...base,
            color: "#fff"
        }),

        singleValue: (base) => ({
            ...base,
            color: "#fff"
        }),

        placeholder: (base) => ({
            ...base,
            color: "#b3b3b3"
        }),

        menu: (base) => ({
            ...base,
            backgroundColor: "#000",
            zIndex: 9999
        }),

        option: (base, state) => ({
            ...base,
            backgroundColor: state.isSelected
                ? "#333"
                : state.isFocused
                  ? "#222"
                  : "#000",
            color: "#fff",
            cursor: "pointer"
        }),

        groupHeading: (base) => ({
            ...base,
            color: "#fbbf24",
            backgroundColor: "#111827",
            fontSize: "16px",
            fontWeight: 900,
            padding: "10px 14px",
            borderBottom: "2px solid #fbbf24"
        }),

        dropdownIndicator: (base) => ({
            ...base,
            color: "#fff"
        }),

        clearIndicator: (base) => ({
            ...base,
            color: "#fff"
        }),

        indicatorSeparator: (base) => ({
            ...base,
            backgroundColor: "#333"
        })
    }}
/>
                            </div>

                            <div className="form-field form-field--wide">
                                <label htmlFor="role">
                                    Role <span>*</span>
                                </label>

                            <CreatableSelect
    inputId="role"
    name="role"
    options={[
        {
            label: "Technology",
            options: [
                "Software Engineer",
                "Backend Developer",
                "Frontend Developer",
                "Full Stack Developer",
                ".NET Developer",
                "Java Developer",
                "Python Developer",
                "React Developer",
                "Mobile App Developer",
                "DevOps Engineer",
                "Cloud Engineer",
                "Data Engineer",
                "Data Analyst",
                "Data Scientist",
                "Machine Learning Engineer",
                "AI Engineer",
                "Cybersecurity Analyst",
                "Network Engineer",
                "Systems Administrator",
                "Database Administrator",
                "QA Engineer",
                "UI/UX Designer",
                "Product Manager",
                "Project Manager",
                "Business Analyst",
                "Technical Support Specialist"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Finance and Accounting",
            options: [
                "Accountant",
                "Financial Analyst",
                "Auditor",
                "Finance Manager",
                "Bank Teller",
                "Investment Analyst"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Marketing and Design",
            options: [
                "Marketing Specialist",
                "Digital Marketing Specialist",
                "Marketing Manager",
                "SEO Specialist",
                "Social Media Manager",
                "Content Writer",
                "Graphic Designer"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Sales and Customer Service",
            options: [
                "Sales Representative",
                "Sales Manager",
                "Account Manager",
                "Business Development Manager",
                "Customer Service Representative",
                "Customer Success Manager"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Human Resources and Administration",
            options: [
                "Human Resources Specialist",
                "Human Resources Manager",
                "Recruiter",
                "Talent Acquisition Specialist",
                "Office Administrator",
                "Executive Assistant",
                "Administrative Assistant"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Healthcare",
            options: [
                "Registered Nurse",
                "Nurse",
                "Doctor",
                "Pharmacist",
                "Physical Therapist",
                "Occupational Therapist",
                "Medical Laboratory Technician",
                "Radiology Technician",
                "Healthcare Administrator"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Medical Doctors",
            options: [
                "General Practitioner",
                "Family Medicine Physician",
                "Internal Medicine Physician",
                "Pediatrician",
                "Cardiologist",
                "Dermatologist",
                "Neurologist",
                "Psychiatrist",
                "Radiologist",
                "Anesthesiologist",
                "Emergency Medicine Physician",
                "General Surgeon",
                "Orthopedic Surgeon",
                "Ophthalmologist",
                "Obstetrician and Gynecologist",
                "Urologist",
                "Oncologist",
                "Endocrinologist",
                "Gastroenterologist",
                "Pulmonologist",
                "Nephrologist",
                "Pathologist",
                "ENT Specialist"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Dentistry",
            options: [
                "Dentist",
                "General Dentist",
                "Orthodontist",
                "Oral Surgeon",
                "Pediatric Dentist",
                "Periodontist",
                "Endodontist",
                "Prosthodontist",
                "Dental Hygienist",
                "Dental Assistant",
                "Dental Technician"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Engineering and Construction",
            options: [
                "Civil Engineer",
                "Mechanical Engineer",
                "Electrical Engineer",
                "Chemical Engineer",
                "Industrial Engineer",
                "Biomedical Engineer",
                "Architect",
                "Construction Manager"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Education",
            options: [
                "Teacher",
                "School Teacher",
                "University Lecturer",
                "Teaching Assistant",
                "Academic Advisor"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Legal",
            options: [
                "Lawyer",
                "Legal Assistant",
                "Compliance Officer"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Supply Chain and Operations",
            options: [
                "Supply Chain Specialist",
                "Procurement Specialist",
                "Logistics Coordinator",
                "Warehouse Manager",
                "Operations Manager"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Hospitality and Travel",
            options: [
                "Hotel Manager",
                "Restaurant Manager",
                "Chef",
                "Receptionist",
                "Travel Agent"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Coaching and Fitness",
            options: [
                "Coach",
                "Life Coach",
                "Career Coach",
                "Business Coach",
                "Executive Coach",
                "Sports Coach",
                "Football Coach",
                "Basketball Coach",
                "Swimming Coach",
                "Fitness Coach",
                "Personal Trainer",
                "Fitness Instructor",
                "Gym Instructor",
                "Strength and Conditioning Coach",
                "Yoga Instructor",
                "Pilates Instructor"
            ].map((role) => ({
                value: role,
                label: role
            }))
        },
        {
            label: "Translation and Languages",
            options: [
                "Translator",
                "Interpreter",
                "Freelance Translator",
                "Legal Translator",
                "Medical Translator",
                "Technical Translator",
                "Literary Translator",
                "Certified Translator",
                "Conference Interpreter",
                "Simultaneous Interpreter",
                "Localization Specialist",
                "Localization Manager",
                "Translation Project Manager",
                "Language Specialist",
                "Linguist",
                "Subtitler",
                "Proofreader",
                "Editor"
            ].map((role) => ({
                value: role,
                label: role
            }))
        }
    ]}
    value={
        searchForm.role
            ? {
                  value: searchForm.role,
                  label: searchForm.role
              }
            : null
    }
    onChange={(selectedOption) => {
        setSearchForm((previous) => ({
            ...previous,
            role: selectedOption?.value ?? ""
        }));
    }}
    onCreateOption={(typedRole) => {
        const cleanRole = typedRole.trim();

        if (!cleanRole) {
            return;
        }

        setSearchForm((previous) => ({
            ...previous,
            role: cleanRole
        }));
    }}
    placeholder="Select or type a role"
    formatCreateLabel={(typedRole) => `Use "${typedRole}"`}
    noOptionsMessage={({ inputValue }) =>
        inputValue
            ? `Press Enter to use "${inputValue}"`
            : "No roles available"
    }
    isSearchable
    isClearable
    openMenuOnClick
    openMenuOnFocus
    closeMenuOnSelect
    blurInputOnSelect={false}
    isDisabled={searchingJobs}
    menuPortalTarget={
        typeof document !== "undefined"
            ? document.body
            : undefined
    }
    menuPosition="fixed"
   styles={{
    control: (base, state) => ({
        ...base,
        minHeight: "48px",
        borderRadius: "12px",
        backgroundColor: "#000",
        borderColor: state.isFocused ? "#444" : "#333",
        boxShadow: state.isFocused
            ? "0 0 0 2px rgba(255,255,255,0.15)"
            : "none",
        "&:hover": {
            borderColor: "#555"
        }
    }),
    valueContainer: (base) => ({
        ...base,
        padding: "0 14px"
    }),
    input: (base) => ({
        ...base,
        color: "#fff"
    }),
    singleValue: (base) => ({
        ...base,
        color: "#fff"
    }),
    placeholder: (base) => ({
        ...base,
        color: "#b3b3b3"
    }),
    menu: (base) => ({
        ...base,
        backgroundColor: "#000",
        color: "#fff",
        zIndex: 9999
    }),
    menuPortal: (base) => ({
        ...base,
        zIndex: 9999
    }),
    option: (base, state) => ({
        ...base,
        backgroundColor: state.isFocused
            ? "#222"
            : state.isSelected
            ? "#333"
            : "#000",
        color: "#fff",
        cursor: "pointer",
        ":active": {
            backgroundColor: "#444"
        }
    }),
groupHeading: (base) => ({
    ...base,
    color: "#a78bfa",          // Blue (change to any color you like)
    fontWeight: 800,
    fontSize: "15px",          // Larger text
    letterSpacing: "0.8px",
    textTransform: "uppercase",
    backgroundColor: "#111827",
    padding: "10px 14px",
    borderBottom: "1px solid #374151",
    marginBottom: "4px"
}),
    dropdownIndicator: (base) => ({
        ...base,
        color: "#fff",
        "&:hover": {
            color: "#fff"
        }
    }),
    clearIndicator: (base) => ({
        ...base,
        color: "#fff",
        "&:hover": {
            color: "#fff"
        }
    }),
    indicatorSeparator: (base) => ({
        ...base,
        backgroundColor: "#333"
    })
}}
/>
                            </div>

                            <div className="form-field">
                                <label htmlFor="workType">
                                    Work mode <span>*</span>
                                </label>

                                <select
                                    id="workType"
                                    name="workType"
                                    value={
                                        searchForm.workType
                                    }
                                    onChange={
                                        handleSearchInputChange
                                    }
                                    disabled={
                                        searchingJobs
                                    }
                                >
                                    <option value="">
                                        Select work mode
                                    </option>

                                    <option value="On-site">
                                        On-site
                                    </option>

                                    <option value="Remote">
                                        Remote
                                    </option>

                                    <option value="Hybrid">
                                        Hybrid
                                    </option>
                                </select>
                            </div>

                            <div className="form-field">
                                <label htmlFor="employmentType">
                                    Employment type{" "}
                                    <span>*</span>
                                </label>

                                <select
                                    id="employmentType"
                                    name="employmentType"
                                    value={
                                        searchForm.employmentType
                                    }
                                    onChange={
                                        handleSearchInputChange
                                    }
                                    disabled={
                                        searchingJobs
                                    }
                                >
                                    <option value="">
                                        Select employment type
                                    </option>

                                    <option value="Full-time">
                                        Full-time
                                    </option>

                                    <option value="Part-time">
                                        Part-time
                                    </option>

                                    <option value="Contract">
                                        Contract
                                    </option>

                                    <option value="Internship">
                                        Internship
                                    </option>
                                </select>
                            </div>

                            <button
    type="submit"
    className="dashboard-primary-button search-button"
    disabled={searchingJobs}
>
    {searchingJobs
        ? loadingMessages[loadingMessageIndex]
        : "Search Jobs"}
</button>
                        </form>
                    </section>

                    {!hasSearched && (
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
                                    <span>⌕</span>
                                </div>
                            </div>

                            <p className="empty-state-label">
                                Start your search
                            </p>

                            <h2>
                                Relevant jobs are one
                                search away
                            </h2>

                            <p className="empty-state-description">
                                Enter your preferences to
                                find available jobs. Match
                                information remains hidden
                                until you explicitly press
                                Show Score.
                            </p>

                            
                        </section>
                    )}

                    {hasSearched &&
                        !searchingJobs &&
                        jobs.length === 0 && (
                            <section className="dashboard-empty-state">
                                <p className="empty-state-label">
                                    No results
                                </p>

                                <h2>
                                    No matching jobs were
                                    found
                                </h2>

                                <p className="empty-state-description">
                                    Try changing the role,
                                    location, work mode, or
                                    employment type.
                                </p>
                            </section>
                        )}

                    {jobs.length > 0 && (
                        <section
                            style={{
                                display: "grid",
                                gap: "18px",
                            }}
                        >
                            {jobs.map((job) => {
                                const scoreWasRevealed =
                                    revealedMatchJobIds.has(
                                        job.jobId
                                    );

                                const scoreIsAvailable =
                                    job.matchScore !== null &&
                                    job.matchScore !==
                                        undefined;

                                const isCalculating =
                                    calculatingJobIds.has(
                                        job.jobId
                                    );

                                const matchError =
                                    matchErrorsByJobId[
                                        job.jobId
                                    ];

                                const isSaving =
                                    savingJobIds.has(
                                        job.jobId
                                    );

                                const isSaved =
                                    savedJobIds.has(
                                        job.jobId
                                    );

                                const isApplying =
                                    applyingJobIds.has(
                                        job.jobId
                                    );

                                const isApplied =
                                    appliedJobIds.has(
                                        job.jobId
                                    );

                                return (
                                    <article
                                        key={job.jobId}
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
                                                    <p
                                                        style={{
                                                            margin: "0 0 10px",
                                                            color: "var(--dashboard-muted)",
                                                            fontSize: "14px",
                                                            display: "flex",
                                                            alignItems: "center",
                                                            gap: "6px",
                                                        }}
                                                    >
                                                        📍
                                                        {[job.city, job.country]
                                                            .filter(Boolean)
                                                            .join(", ")}
                                                    </p>
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

                                                <div
                                                    style={{
                                                        display:
                                                            "flex",
                                                        gap: "8px",
                                                        flexWrap:
                                                            "wrap",
                                                    }}
                                                >
                                                    {job.workMode && (
                                                        <span className="feature-chip">
                                                            {
                                                                job.workMode
                                                            }
                                                        </span>
                                                    )}

                                                    {job.employmentType && (
                                                        <span className="feature-chip">
                                                            {
                                                                job.employmentType
                                                            }
                                                        </span>
                                                    )}
                                                </div>
                                            </div>

                                            <p
                                                style={{
                                                    margin: 0,
                                                    color:
                                                        "var(--dashboard-muted)",
                                                    fontSize:
                                                        "13px",
                                                    lineHeight:
                                                        1.7,
                                                }}
                                            >
                                                {job.description
                                                    ? job.description.length >
                                                      420
                                                        ? `${job.description.slice(
                                                              0,
                                                              420
                                                          )}...`
                                                        : job.description
                                                    : "No job description was provided."}
                                            </p>

                                            <span
                                                style={{
                                                    color:
                                                        "var(--dashboard-muted)",
                                                    fontSize:
                                                        "12px",
                                                }}
                                            >
                                                Posted:{" "}
                                                {formatPostedDate(
                                                    job.postedDate
                                                )}
                                            </span>

                                            {scoreWasRevealed &&
                                                scoreIsAvailable && (
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
                                                                Match
                                                                score
                                                            </p>

                                                            <strong
                                                                style={{
                                                                    fontSize:
                                                                        "30px",
                                                                }}
                                                            >
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
                                                )}

                                            {matchError && (
                                                <div
                                                    role="alert"
                                                    style={{
                                                        padding:
                                                            "14px 16px",
                                                        border:
                                                            "1px solid rgba(255, 105, 135, 0.45)",
                                                        borderRadius:
                                                            "14px",
                                                        background:
                                                            "rgba(255, 105, 135, 0.08)",
                                                        color:
                                                            "var(--dashboard-text)",
                                                        fontSize:
                                                            "13px",
                                                        lineHeight: 1.6,
                                                    }}
                                                >
                                                    {matchError}
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
                                                        isCalculating
                                                    }
                                                    onClick={() =>
                                                        handleCalculateMatch(
                                                            job.jobId
                                                        )
                                                    }
                                                >
                                                    {isCalculating
                                                        ? "Calculating..."
                                                        : scoreWasRevealed
                                                          ? "Refresh Score"
                                                          : "Show Score"}
                                                </button>

                                                <button
                                                    type="button"
                                                    className="dashboard-primary-button"
                                                    disabled={
                                                        isSaving
                                                    }
                                                    onClick={() =>
                                                        handleToggleSavedJob(
                                                            job.jobId
                                                        )
                                                    }
                                                >
                                                    {isSaving
                                                        ? isSaved
                                                            ? "Removing..."
                                                            : "Saving..."
                                                        : isSaved
                                                          ? "Unsave"
                                                          : "Save Job"}
                                                </button>

                                                <button
                                                    type="button"
                                                    className="dashboard-primary-button"
                                                    disabled={
                                                        isApplying ||
                                                        isApplied
                                                    }
                                                    onClick={() =>
                                                        handleApplyForJob(
                                                            job
                                                        )
                                                    }
                                                >
                                                    {isApplying
                                                        ? "Applying..."
                                                        : isApplied
                                                          ? "Applied"
                                                          : "Apply"}
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

export default DashboardPage;