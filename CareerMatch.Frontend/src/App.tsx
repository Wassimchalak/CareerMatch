import {
    BrowserRouter,
    Route,
    Routes,
} from "react-router-dom";

import LandingPage from "./pages/LandingPage";
import DashboardPage from "./pages/DashboardPage";
import SavedJobsPage from "./pages/SavedJobsPage";
import AuthPage from "./pages/AuthPage";
import ResetPasswordPage from "./pages/ResetPasswordPage";
import ForgotPasswordPage from "./pages/ForgotPasswordPage";
import AppliedJobsPage from "./pages/AppliedJobsPage";
function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route
                    path="/"
                    element={<LandingPage />}
                />

                <Route
                    path="/auth"
                    element={<AuthPage />}
                />

                <Route
                    path="/dashboard"
                    element={<DashboardPage />}
                />

                <Route
                    path="/saved-jobs"
                    element={<SavedJobsPage />}
                />

                <Route
                    path="/reset-password"
                    element={<ResetPasswordPage />}
                />

                <Route
                    path="/forgot-password"
                    element={<ForgotPasswordPage />}
                />
                        <Route
            path="/applied-jobs"
            element={<AppliedJobsPage />}
        />
            </Routes>
        </BrowserRouter>
    );
}

export default App;