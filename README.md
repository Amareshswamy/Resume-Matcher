# 🚀 Applicant Tracking System (ATS) - HiringPro

This project is a lightweight, serverless Applicant Tracking System built with **Azure Functions**,**Azure Logic Apps**,**Azure AI foundary**, **Azure Blob Storage**, and a dynamic web UI. It allows recruiters to create, publish, and manage job postings, as well as receive and evaluate candidate applications automatically using AI.

---

## ✨ Features

✅ **Job Management**
- Create new job postings with unique IDs and detailed descriptions
- View all current job openings
- Delete jobs when positions are filled

✅ **Dynamic Job Listings**
- Job openings are stored as JSON in Azure Blob Storage
- The public jobs page dynamically fetches and displays them
- Each posting auto-formats into structured sections (About, Responsibilities, etc.)

✅ **Resume Scoring and Application Workflow**
- Candidates apply via the job portal
- An Azure Logic App triggers when an application is submitted
- The Logic App calls an Azure Function
  - Fetches the candidate's resume
  - Uses AI prompts to evaluate the resume against the job description
  - Calculates a suitability score
- If the score exceeds a configurable cutoff, an email is sent to the recruiter with the candidate details and resume attached

✅ **Admin Portal**
- Add new jobs through an admin form
- Browse existing postings
- Remove postings as needed via delete actions

✅ **Modern UI**
- Clean, responsive HTML/CSS with clear navigation
- Separate pages for listing jobs and managing them

---

## 🛠️ Tech Stack

- **Frontend:** HTML, CSS, Vanilla JavaScript
- **Backend:** Azure Functions (C#)
- **Storage:** Azure Blob Storage (for job JSON and resumes)
- **Automation:** Azure Logic Apps
- **Email:** Logic Apps email connector (or SendGrid)
- **Deployment:** Azure Static Web Apps / Azure App Service

---

## 📂 Project live link

https://icy-mushroom-0ca8ad80f.1.azurestaticapps.net/

